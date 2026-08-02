import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { queryClient } from '@/config/queryClient'
import { MainLayout } from '@/components/layout/MainLayout'
import { AuthProvider } from '@/features/auth/AuthProvider'
import { RequireAuth } from '@/features/auth/RequireAuth'
import { AnalysisWorkspace } from '@/features/analysis/AnalysisWorkspace'
import { AnalysisDetailPage } from '@/pages/AnalysisDetailPage'
import { CallbackPage } from '@/pages/CallbackPage'
import { HistoryPage } from '@/pages/HistoryPage'
import { LoginPage } from '@/pages/LoginPage'
import { NotFoundPage } from '@/pages/NotFoundPage'

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        {/* Inside the router so the auth state can drive navigation. */}
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/auth/callback" element={<CallbackPage />} />

            <Route
              path="/"
              element={
                <RequireAuth>
                  <MainLayout>
                    <AnalysisWorkspace />
                  </MainLayout>
                </RequireAuth>
              }
            />

            <Route
              path="/analyses"
              element={
                <RequireAuth>
                  <MainLayout>
                    <HistoryPage />
                  </MainLayout>
                </RequireAuth>
              }
            />

            <Route
              path="/analyses/:analysisId"
              element={
                <RequireAuth>
                  <MainLayout>
                    <AnalysisDetailPage />
                  </MainLayout>
                </RequireAuth>
              }
            />

            {/* The old build checked window.location for the callback path; with a
                real router, an unknown path should say so rather than render the app. */}
            <Route path="/index.html" element={<Navigate to="/" replace />} />
            <Route path="*" element={<NotFoundPage />} />
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
