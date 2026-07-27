import type { ReactNode } from 'react'
import { TopNavigation } from './TopNavigation'

export function MainLayout({ children }: { children: ReactNode }) {
  return (
    <div className="d-flex flex-column vh-100 overflow-hidden">
      <TopNavigation />

      {/* min-height:0 lets the scroll container inside actually scroll instead of
          stretching the flex parent past the viewport. */}
      <main className="flex-grow-1 d-flex flex-column overflow-hidden min-h-0">
        {children}
      </main>
    </div>
  )
}
