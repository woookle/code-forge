import React, { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Provider } from 'react-redux'
import { store } from './app/store'
import './index.css'
import App from './App'
import { ConfirmProvider } from './context/ConfirmContext'

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <Provider store={store}>
            <ConfirmProvider>
                <App />
            </ConfirmProvider>
        </Provider>
    </StrictMode>,
)
