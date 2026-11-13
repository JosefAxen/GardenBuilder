import { createBrowserRouter } from 'react-router'
import { AdminPage } from './admin/AdminPage'
import { CustomerFlowPage } from './app/CustomerFlowPage'
import { EditorPage } from './editor/EditorPage'

export const router = createBrowserRouter([
  { path: '/', element: <CustomerFlowPage /> },
  { path: '/editor', element: <EditorPage /> },
  { path: '/admin', element: <AdminPage /> },
])
