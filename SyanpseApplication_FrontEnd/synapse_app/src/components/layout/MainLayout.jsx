import TopNavigation from "./TopNavigation";

const MainLayout = ({ children }) => {
  return (
    <div
      className="d-flex flex-column vh-100"
      style={{ fontFamily: "system-ui, -apple-system, sans-serif" }}
    >
      <TopNavigation />
      {/* The flex-grow-1 ensures this main area takes up all space below the nav */}
      <main className="flex-grow-1 d-flex flex-column overflow-hidden bg-dark text-light">
        {children}
      </main>
    </div>
  );
};

export default MainLayout;
