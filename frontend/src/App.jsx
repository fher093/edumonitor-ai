import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from './auth/useAuth.js'
import { ROLES, rutaInicial } from './auth/roles.js'
import ProtectedRoute from './auth/ProtectedRoute.jsx'
import Layout from './components/Layout.jsx'
import Login from './pages/Login.jsx'
import DocenteHome from './pages/DocenteHome.jsx'
import AdminHome from './pages/AdminHome.jsx'
import Actividades from './pages/Actividades.jsx'
import Dashboard from './pages/Dashboard.jsx'
import FichaEstudiante from './pages/FichaEstudiante.jsx'
import Alertas from './pages/Alertas.jsx'
import Analisis from './pages/Analisis.jsx'
import AdminReglas from './pages/AdminReglas.jsx'
import NoAutorizado from './pages/NoAutorizado.jsx'

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
          <Route path="/cursos" element={<DocenteHome />} />
          <Route path="/matricula" element={<DocenteHome />} />
          <Route path="/actividades" element={<Actividades />} />
          <Route path="/dashboard" element={<Dashboard />} />
          <Route path="/alertas" element={<Alertas />} />
          <Route path="/analisis" element={<Analisis />} />
          <Route path="/ficha/:cursoId/:estudianteId" element={<FichaEstudiante />} />
          <Route path="/informe" element={<DocenteHome />} />
          <Route path="/agente" element={<DocenteHome />} />
        </Route>

        {/* Rutas Administrador Técnico */}
        <Route element={<ProtectedRoute roles={[ROLES.ADMIN]} />}>
          <Route path="/admin" element={<AdminHome />} />
          <Route path="/admin/cursos" element={<AdminHome />} />
          <Route path="/admin/actividades" element={<Actividades />} />
          <Route path="/admin/reglas-alertas" element={<AdminReglas />} />
          <Route path="/admin/auditoria" element={<AdminHome />} />
          <Route path="/admin/servicios" element={<AdminHome />} />
          <Route path="/admin/dashboard" element={<Dashboard />} />
          <Route path="/admin/alertas" element={<Alertas />} />
          <Route path="/admin/analisis" element={<Analisis />} />
          <Route path="/admin/ficha/:cursoId/:estudianteId" element={<FichaEstudiante />} />
          <Route path="/admin/informe" element={<AdminHome />} />
          <Route path="/admin/agente" element={<AdminHome />} />
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
