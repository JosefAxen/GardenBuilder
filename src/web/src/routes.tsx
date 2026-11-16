import { createBrowserRouter } from 'react-router'
import { AdminPage } from './admin/AdminPage'
import { RequireStaff } from './admin/RequireStaff'
import { CustomerFlowPage } from './app/CustomerFlowPage'
import { EditorPage } from './editor/EditorPage'

export const router = createBrowserRouter([
  { path: '/', element: <CustomerFlowPage /> },
  { path: '/editor', element: <EditorPage /> },
  {
    path: '/admin',
    element: <RequireStaff>{(me, signOut) => <AdminPage me={me} onLogout={signOut} />}</RequireStaff>,
  },
])
