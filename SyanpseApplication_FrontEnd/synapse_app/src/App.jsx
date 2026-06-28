import { AuthProvider } from "./features/auth/AuthProvider";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import MainLayout from "./components/layout/MainLayout";
import AnalysisBoard from "./features/analysis/AnalysisBoard";
import "bootstrap/dist/css/bootstrap.min.css";

const queryClient = new QueryClient();

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <MainLayout>
          <AnalysisBoard />
        </MainLayout>
      </AuthProvider>
    </QueryClientProvider>
  );
}
