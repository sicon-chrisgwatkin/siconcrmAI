import { StrictMode, useState } from 'react'
import { createRoot } from 'react-dom/client'
import {
  BrowserRouter,
  Navigate,
  Outlet,
  Route,
  Routes,
} from 'react-router-dom'

import NavHeader from './components/NavHeader'
import './index.css'
import { mockModeEnabled } from './lib/api'
import ChatPanel from './pages/Chat'
import RecordViewer from './pages/Record'

function WorkspaceHome() {
  return (
    <section className="workspace-panel">
      <h1>CRM Assistant Workspace</h1>
      <p>
        Use the right-hand chat panel to draft actions from natural language, collect
        missing fields, and confirm execution.
      </p>
      <ul>
        <li>Supported entities: task, opportunity, company, person, sales/purchase orders</li>
        <li>The preview card shows fields and line items before execution</li>
        <li>After confirmation, records open in this area for review</li>
      </ul>
    </section>
  )
}

function AppShell() {
  const [panelVisible, setPanelVisible] = useState(true)

  return (
    <div className="app-shell">
      <NavHeader
        panelVisible={panelVisible}
        onTogglePanel={() => {
          setPanelVisible((current) => !current)
        }}
        mockModeEnabled={mockModeEnabled}
      />
      <div className="app-body">
        <main className="app-main">
          <Outlet />
        </main>
        {panelVisible && (
          <aside className="chat-dock" aria-label="Assistant panel">
            <ChatPanel />
          </aside>
        )}
      </div>
    </div>
  )
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<AppShell />}>
          <Route index element={<WorkspaceHome />} />
          <Route path="record/:type/:id" element={<RecordViewer />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  </StrictMode>,
)
