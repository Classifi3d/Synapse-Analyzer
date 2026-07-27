import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import 'bootstrap/dist/css/bootstrap.min.css'
import './index.css'
import App from './App'

const container = document.getElementById('root')

if (!container) {
  throw new Error('The #root element is missing from index.html.')
}

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
