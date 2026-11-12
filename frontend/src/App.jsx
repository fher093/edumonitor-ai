import { Navigate, Route, Routes, useParams } from 'react-router-dom'
import { useAuth } from './auth/useAuth.js'
import { ROLES, rutaInicial } from './auth/roles.js'
import ProtectedRoute from './auth/ProtectedRoute.jsx'
import Layout from './components/Layout.jsx'
import Login from './pages/Login.jsx'
import AdminHome from './pages/AdminHome.jsx'
import Actividades from './pages/Actividades.jsx'
import Resumen from './pages/Resumen.jsx'
import Estudiantes from './pages/Estudiantes.jsx'
import MisCursos from './pages/MisCursos.jsx'
import ModuloIA from './pages/ModuloIA.jsx'
import Alertas from './pages/Alertas.jsx'
import Analisis from './pages/Analisis.jsx'
import AdminReglas from './pages/AdminReglas.jsx'
import AdminMoodle from './pages/AdminMoodle.jsx'
import AdminDocentes from './pages/AdminDocentes.jsx'
import NoAutorizado from './pages/NoAutorizado.jsx'

// Enlaces antiguos de la ficha individual: ahora es el panel derecho de Estudiantes.
function RedireccionFicha() {
  const { estudianteId } = useParams()
  return <Navigate to={`/estudiantes?id=${estudianteId}`} replace />
}

function Inicio() {
  const { usuario } = useAuth()
  return <Navigate to={usuario ? rutaInicial(usuario.rol) : '/login'} replace />
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />

      <Route element={<Layout />}>
        {/* Rutas Docente */}
        <Route element={<ProtectedRoute roles={[ROLES.DOCENTE]} />}>
          <Route path="/inicio" element={<Resumen />} />
          <Route path="/estudiantes" element={<Estudiantes />} />
          <Route path="/actividades" element={<Actividades />} />
          <Route path="/alertas" element={<Alertas />} />
          <Route path="/analisis" element={<Analisis />} />
          <Route path="/cursos" element={<MisCursos />} />
          <Route path="/agente" element={<ModuloIA tipo="agente" />} />
          <Route path="/informe" element={<ModuloIA tipo="informe" />} />
          {/* Rutas anteriores al rediseño */}
          <Route path="/dashboard" element={<Navigate to="/inicio" replace />} />
          <Route path="/matricula" element={<Navigate to="/estudiantes" replace />} />
          <Route path="/ficha/:cursoId/:estudianteId" element={<RedireccionFicha />} />
        </Route>

        {/* Rutas Administrador Técnico: cuentas, integración con Moodle, reglas y estado técnico.
            No accede a datos académicos (notas, estudiantes): eso es del docente propietario. */}
        <Route element={<ProtectedRoute roles={[ROLES.ADMIN]} />}>
          <Route path="/admin" element={<AdminDocentes />} />
          <Route path="/admin/moodle" element={<AdminMoodle />} />
          <Route path="/admin/reglas-alertas" element={<AdminReglas />} />
          <Route path="/admin/servicios" element={<AdminHome />} />
          <Route path="/admin/auditoria" element={<AdminHome />} />
        </Route>

        <Route element={<ProtectedRoute />}>
          <Route path="/no-autorizado" element={<NoAutorizado />} />
        </Route>
      </Route>

      <Route path="/" element={<Inicio />} />
      <Route path="*" element={<Inicio />} />
    </Routes>
  )
}
