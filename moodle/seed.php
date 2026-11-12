<?php
// EduMonitor AI — preparación del Moodle local de demostración (RF-02 / RF-03).
//
// Se ejecuta automáticamente al arrancar el contenedor (POST_CONFIGURE_COMMANDS) y es idempotente:
//   1. Habilita los servicios web y el protocolo REST.
//   2. Crea el usuario técnico "edumonitor_ws" con un rol de solo lectura (mínimo privilegio).
//   3. Crea el servicio externo "EduMonitor AI" con las funciones que usa MoodleService.
//   4. Registra el token técnico con el valor de la variable MOODLE_WS_TOKEN.
//   5. Siembra cursos, docentes, estudiantes, ítems de calificación por componente UTA, notas y entregas.
//
// Ejecución manual:  docker compose exec moodle php /opt/edumonitor/seed.php

define('CLI_SCRIPT', true);
require('/var/www/html/config.php');
require_once($CFG->libdir . '/gradelib.php');
require_once($CFG->libdir . '/grade/grade_category.php');
require_once($CFG->libdir . '/grade/grade_item.php');
require_once($CFG->dirroot . '/course/lib.php');
require_once($CFG->dirroot . '/user/lib.php');

\core\session\manager::set_user(get_admin());

function info(string $m): void { mtrace('[edumonitor] ' . $m); }

$token = trim((string) getenv('MOODLE_WS_TOKEN'));
if (!preg_match('/^[a-f0-9]{32}$/', $token)) {
    info('MOODLE_WS_TOKEN no está definido o no tiene 32 caracteres hexadecimales; se omite la preparación.');
    exit(0);
}

$sistema = context_system::instance();

// ---------------------------------------------------------------------------
// 1. Servicios web REST
// ---------------------------------------------------------------------------
set_config('enablewebservices', 1);
$protocolos = array_filter(explode(',', (string) get_config('core', 'webserviceprotocols')));
if (!in_array('rest', $protocolos)) {
    $protocolos[] = 'rest';
    set_config('webserviceprotocols', implode(',', $protocolos));
}

// ---------------------------------------------------------------------------
// 2. Usuario técnico + rol de solo lectura
// ---------------------------------------------------------------------------
function asegurar_usuario(array $datos): stdClass {
    global $DB, $CFG;
    if ($u = $DB->get_record('user', ['username' => $datos['username'], 'deleted' => 0])) {
        return $u;
    }
    $u = (object) array_merge([
        'auth' => 'manual',
        'confirmed' => 1,
        'mnethostid' => $CFG->mnet_localhost_id,
        'password' => 'Edu-' . bin2hex(random_bytes(6)) . '!',
        'lang' => 'en',
    ], $datos);
    $u->id = user_create_user($u, true, false);
    return $DB->get_record('user', ['id' => $u->id]);
}

$tecnico = asegurar_usuario([
    'username' => 'edumonitor_ws',
    'firstname' => 'EduMonitor',
    'lastname' => 'Servicio técnico',
    'email' => 'ws@edumonitor.local',
]);

$rolid = $DB->get_field('role', 'id', ['shortname' => 'edumonitorws']);
if (!$rolid) {
    $rolid = create_role('EduMonitor (servicio web de solo lectura)', 'edumonitorws',
        'Rol técnico de EduMonitor AI: consulta cursos, participantes, calificaciones y entregas. No modifica datos.');
    set_role_contextlevels($rolid, [CONTEXT_SYSTEM]);
}
$capacidades = [
    'webservice/rest:use',
    'moodle/course:view',
    'moodle/course:viewhiddencourses',
    'moodle/category:viewcourselist',
    'moodle/category:viewhiddencategories',
    'moodle/course:viewparticipants',
    'moodle/course:enrolreview',
    'moodle/user:viewdetails',
    'moodle/user:viewhiddendetails',
    'moodle/course:useremail',
    'moodle/site:viewuseridentity',
    'moodle/site:accessallgroups',
    'moodle/grade:view',
    'moodle/grade:viewall',
    'moodle/grade:viewhidden',
    'gradereport/user:view',
    'mod/assign:view',
    'mod/assign:grade',
    'mod/assign:viewgrades',
    'mod/quiz:view',
    'mod/quiz:viewreports',
];
foreach ($capacidades as $cap) {
    if (get_capability_info($cap)) {
        assign_capability($cap, CAP_ALLOW, $rolid, $sistema->id, true);
    }
}
if (!$DB->record_exists('role_assignments', ['roleid' => $rolid, 'userid' => $tecnico->id, 'contextid' => $sistema->id])) {
    role_assign($rolid, $tecnico->id, $sistema->id);
}
// Mostrar el número de identificación (cédula) a quien tenga permiso.
set_config('showuseridentity', 'email,idnumber');
accesslib_clear_all_caches(true);

// ---------------------------------------------------------------------------
// 3. Servicio externo con las funciones que consume MoodleService
// ---------------------------------------------------------------------------
$funciones = [
    'core_webservice_get_site_info',
    'core_course_get_courses',
    'core_course_get_categories',
    'core_enrol_get_enrolled_users',
    'core_grades_get_grade_tree',
    'gradereport_user_get_grade_items',
    'mod_assign_get_assignments',
    'mod_assign_get_submissions',
    'mod_quiz_get_quizzes_by_courses',
];
$servicio = $DB->get_record('external_services', ['shortname' => 'edumonitor']);
if (!$servicio) {
    $servicio = (object) [
        'name' => 'EduMonitor AI',
        'shortname' => 'edumonitor',
        'enabled' => 1,
        'restrictedusers' => 1,
        // Necesario para descargar las fotos de perfil por webservice/pluginfile.php.
        // MoodleService solo pide rutas de iconos de usuario (<ctx>/user/icon/...) y el token nunca sale del servidor.
        'downloadfiles' => 1,
        'uploadfiles' => 0,
        'timecreated' => time(),
        'timemodified' => time(),
    ];
    $servicio->id = $DB->insert_record('external_services', $servicio);
} else if (empty($servicio->downloadfiles)) {
    // Instalaciones creadas antes de sincronizar fotos: habilita la descarga de archivos.
    $DB->set_field('external_services', 'downloadfiles', 1, ['id' => $servicio->id]);
    $DB->set_field('external_services', 'timemodified', time(), ['id' => $servicio->id]);
    info('Servicio "EduMonitor AI": descarga de archivos habilitada (fotos de perfil).');
}
foreach ($funciones as $f) {
    if (!$DB->record_exists('external_services_functions', ['externalserviceid' => $servicio->id, 'functionname' => $f])) {
        $DB->insert_record('external_services_functions', (object) ['externalserviceid' => $servicio->id, 'functionname' => $f]);
    }
}
if (!$DB->record_exists('external_services_users', ['externalserviceid' => $servicio->id, 'userid' => $tecnico->id])) {
    $DB->insert_record('external_services_users', (object) [
        'externalserviceid' => $servicio->id, 'userid' => $tecnico->id, 'timecreated' => time(),
    ]);
}

// ---------------------------------------------------------------------------
// 4. Token técnico con valor conocido (el mismo que recibe MoodleService)
// ---------------------------------------------------------------------------
$registro = $DB->get_record('external_tokens', ['externalserviceid' => $servicio->id, 'userid' => $tecnico->id]);
if (!$registro) {
    $generado = \core_external\util::generate_token(EXTERNAL_TOKEN_PERMANENT, $servicio, $tecnico->id, $sistema, 0, '', 'EduMonitor AI');
    $registro = $DB->get_record('external_tokens', ['token' => $generado]);
}
if ($registro->token !== $token) {
    $DB->delete_records('external_tokens', ['token' => $token]);
    $DB->set_field('external_tokens', 'token', $token, ['id' => $registro->id]);
}
info('Servicio web REST listo (usuario técnico edumonitor_ws, servicio "EduMonitor AI").');

// ---------------------------------------------------------------------------
// 5. Datos académicos de demostración
// ---------------------------------------------------------------------------
if ($DB->record_exists('course', ['shortname' => 'SW-401'])) {
    info('Datos de demostración ya existentes; no se vuelven a crear.');
    exit(0);
}

$generador = \core\test\phpunit\phpunit_util::get_data_generator();
$ahora = time();
$dia = DAYSECS;

$categoria = $DB->get_record('course_categories', ['idnumber' => 'periodo-2026-B'])
    ?: $generador->create_category(['name' => '2026-B', 'idnumber' => 'periodo-2026-B']);

$docente = asegurar_usuario([
    'username' => 'docente', 'firstname' => 'Docente', 'lastname' => 'de prueba',
    'email' => 'docente@edumonitor.local', 'idnumber' => '1700000001',
]);
$docenteSinCuenta = asegurar_usuario([
    'username' => 'lmartinez', 'firstname' => 'Luis', 'lastname' => 'Martínez',
    'email' => 'luis.martinez@uta.edu.ec', 'idnumber' => '1700000002',
]);

$personas = [
    ['1801234567', 'Juan', 'Pérez'], ['1802345678', 'María', 'Gómez'], ['1803456789', 'Carlos', 'López'],
    ['1804567890', 'Ana', 'Torres'], ['1805678901', 'David', 'Morales'], ['1806789012', 'Lucía', 'Ramírez'],
    ['1807890123', 'Pedro', 'Castillo'], ['1808901234', 'Sofía', 'Vega'],
];
$estudiantes = [];
foreach ($personas as [$cedula, $nombre, $apellido]) {
    $usuario = strtolower(strtr($nombre . '.' . $apellido,
        ['á' => 'a', 'é' => 'e', 'í' => 'i', 'ó' => 'o', 'ú' => 'u', 'ñ' => 'n']));
    $estudiantes[] = asegurar_usuario([
        'username' => $usuario, 'firstname' => $nombre, 'lastname' => $apellido,
        'email' => $usuario . '@uta.edu.ec', 'idnumber' => $cedula,
    ]);
}

/** Crea Parcial 1 / Parcial 2 con las tres subcategorías UTA y devuelve sus ids. */
function crear_categorias_uta(int $cursoid): array {
    $raiz = grade_category::fetch_course_category($cursoid);
    $componentes = [
        'CD' => ['Aprendizaje en contacto con el docente', 40],
        'PE' => ['Aprendizaje práctico-experimental', 30],
        'AA' => ['Aprendizaje autónomo', 30],
    ];
    $ids = [];
    foreach ([1, 2] as $p) {
        $parcial = new grade_category(['courseid' => $cursoid, 'fullname' => "Parcial $p"], false);
        $parcial->insert();
        $parcial->set_parent($raiz->id);
        foreach ($componentes as $clave => [$nombre, $peso]) {
            $cat = new grade_category(['courseid' => $cursoid, 'fullname' => $nombre], false);
            $cat->insert();
            $cat->set_parent($parcial->id);
            $ids["P$p-$clave"] = $cat->id;
        }
    }
    return $ids;
}

/**
 * Crea una actividad (tarea o cuestionario) sobre 10 puntos dentro de la categoría UTA indicada.
 * Devuelve [modulo, instancia, gradeitem].
 */
function crear_actividad($generador, stdClass $curso, array $cats, array $def, int $ahora): array {
    [$nombre, $modulo, $clave, $dias] = $def;
    $fecha = $ahora + $dias * DAYSECS;
    $datos = ['course' => $curso->id, 'name' => $nombre, 'grade' => 10, 'intro' => $nombre];
    if ($modulo === 'assign') {
        $datos += ['duedate' => $fecha, 'allowsubmissionsfromdate' => $fecha - 14 * DAYSECS,
            'assignsubmission_onlinetext_enabled' => 1];
    } else {
        $datos += ['timeopen' => $fecha - 7 * DAYSECS, 'timeclose' => $fecha, 'sumgrades' => 10];
    }
    $instancia = $generador->create_module($modulo, $datos);
    $item = grade_item::fetch(['courseid' => $curso->id, 'itemtype' => 'mod', 'itemmodule' => $modulo,
        'iteminstance' => $instancia->id, 'itemnumber' => 0]);
    $item->set_parent($cats[$clave]);
    $item->grademax = 10;
    $item->idnumber = $clave . '-' . $instancia->cmid;
    $item->update();
    return [$modulo, $instancia, $item];
}

function calificar(array $actividad, int $userid, float $nota): void {
    [$modulo, $instancia] = $actividad;
    grade_update("mod/$modulo", $instancia->course, 'mod', $modulo, $instancia->id, 0,
        ['userid' => $userid, 'rawgrade' => $nota, 'dategraded' => time(), 'datesubmitted' => time()]);
}

/** Entrega sin calificar (estado "completada" en EduMonitor). */
function entregar(array $actividad, int $userid): void {
    global $DB;
    [$modulo, $instancia] = $actividad;
    if ($modulo !== 'assign') return;
    $DB->insert_record('assign_submission', (object) [
        'assignment' => $instancia->id, 'userid' => $userid, 'timecreated' => time(), 'timemodified' => time(),
        'timestarted' => time(), 'status' => 'submitted', 'groupid' => 0, 'attemptnumber' => 0, 'latest' => 1,
    ]);
}

$planCursos = [
    ['SW-401', 'Metodologías Ágiles', $docente, $estudiantes],
    ['SW-402', 'Ingeniería Web', $docente, array_slice($estudiantes, 0, 5)],
    ['SO-301', 'Sistemas Operativos', $docenteSinCuenta, array_slice($estudiantes, 3, 4)],
];

// 7 actividades evaluadas (dos parciales) + 1 futura, como en el módulo académico del Sprint 2.
$actividadesBase = [
    ['Lección 1: Fundamentos', 'quiz', 'P1-CD', -70],
    ['Taller 1: Práctica guiada', 'assign', 'P1-PE', -60],
    ['Tarea 1: Ensayo', 'assign', 'P1-AA', -50],
    ['Lección 2: Evaluación de unidad', 'quiz', 'P1-CD', -40],
    ['Lección 3: Evaluación de unidad', 'quiz', 'P2-CD', -25],
    ['Taller 2: Proyecto aplicado', 'assign', 'P2-PE', -15],
    ['Tarea 2: Portafolio', 'assign', 'P2-AA', -5],
    ['Tarea 3: Informe final', 'assign', 'P2-AA', 10],
];

// Patrones de notas (sobre 10) que reproducen los casos críticos de la demo:
// alto rendimiento, descenso sostenido, atrasos reiterados, nota cero + entregas sin calificar, rendimiento bajo.
// null = sin nota; 'E' = entregada sin calificar.
$patrones = [
    [9.5, 9.0, 8.8, 9.2, 8.5, 9.0, 8.8],
    [8.5, 8.0, 7.5, 6.0, 5.0, 4.0, 3.5],
    [6.0, 5.0, 4.0, 3.0, null, null, null],
    [7.0, 0.0, 6.0, null, 'E', 5.0, 'E'],
    [4.0, 3.0, 5.0, 4.0, 3.0, 2.0, 3.0],
    [7.5, 8.0, 7.0, 8.5, 7.5, 8.0, 'E'],
    [6.5, 6.0, null, 7.0, 6.5, null, 6.0],
    [8.0, 8.5, 9.0, 8.0, 9.5, 9.0, 9.5],
];

$rolEstudiante = $DB->get_field('role', 'id', ['shortname' => 'student']);
$rolDocente = $DB->get_field('role', 'id', ['shortname' => 'editingteacher']);

foreach ($planCursos as $c => [$codigo, $nombre, $profesor, $alumnos]) {
    $curso = $generador->create_course([
        'fullname' => $nombre, 'shortname' => $codigo, 'idnumber' => $codigo . '-2026B',
        'category' => $categoria->id, 'startdate' => $ahora - 90 * $dia, 'enddate' => $ahora + 30 * $dia,
        'enablecompletion' => 1,
    ]);
    $generador->enrol_user($profesor->id, $curso->id, $rolDocente);
    foreach ($alumnos as $a) {
        $generador->enrol_user($a->id, $curso->id, $rolEstudiante);
    }

    $cats = crear_categorias_uta($curso->id);
    $actividades = [];
    foreach ($actividadesBase as $def) {
        $actividades[] = crear_actividad($generador, $curso, $cats, $def, $ahora);
    }

    foreach ($alumnos as $i => $alumno) {
        $patron = $patrones[($i + $c) % count($patrones)];
        foreach ($patron as $j => $valor) {
            if ($valor === null) continue;
            if ($valor === 'E') { entregar($actividades[$j], $alumno->id); continue; }
            calificar($actividades[$j], $alumno->id, (float) $valor);
        }
    }
    info("Curso $codigo ($nombre) creado con " . count($alumnos) . ' estudiantes y ' . count($actividades) . ' actividades.');
}

info('Datos de demostración creados.');
