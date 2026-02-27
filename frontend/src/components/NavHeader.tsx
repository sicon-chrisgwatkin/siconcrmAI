import { Link } from 'react-router-dom'

interface NavHeaderProps {
  panelVisible: boolean
  onTogglePanel: () => void
  mockModeEnabled: boolean
}

function NavHeader({
  panelVisible,
  onTogglePanel,
  mockModeEnabled,
}: NavHeaderProps) {
  return (
    <header className="nav-header">
      <div className="nav-brand">
        <Link to="/" className="nav-title-link">
          Sicon CRM AI Assistant
        </Link>
        <span className="nav-subtitle">Sales / Purchase workflow assistant</span>
      </div>
      <div className="nav-actions">
        {mockModeEnabled ? (
          <span className="badge badge-warning">Mock mode</span>
        ) : (
          <span className="badge">Live API</span>
        )}
        <button type="button" className="button button-secondary" onClick={onTogglePanel}>
          {panelVisible ? 'Hide panel' : 'Show panel'}
        </button>
      </div>
    </header>
  )
}

export default NavHeader
