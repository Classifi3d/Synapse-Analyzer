// src/components/layout/TopNavigation.jsx
import { useContext } from "react";
import { Navbar, Container, Nav, Button } from "react-bootstrap";
import { If, Then, Else } from "react-if";
import { AuthContext } from "../../features/auth/AuthContext";
const TopNavigation = () => {
  const { user, logout } = useContext(AuthContext);

  // Helper to extract initials from the user's name
  const getUserInitials = (name) => {
    if (!name) return "??";
    return name
      .split(" ")
      .map((n) => n[0])
      .join("")
      .toUpperCase()
      .slice(0, 2);
  };

  const handleSsoRedirect = () => {
    window.location.href = `${import.meta.env.VITE_SSO_LOGIN_URL}?redirect=${window.location.origin}/auth/callback`;
  };

  return (
    <Navbar
      bg="dark"
      variant="dark"
      className="border-bottom border-secondary py-2"
    >
      <Container fluid className="px-4">
        <Navbar.Brand
          href="#"
          className="d-flex align-items-center gap-2 fw-bold"
        >
          <i className="bi bi-shield-network text-primary fs-4"></i>
          <span>PacketGuard AI</span>
        </Navbar.Brand>

        <Nav className="ms-auto align-items-center">
          <If condition={!!user}>
            <Then>
              {/* Authenticated State: Initials Avatar Circle */}
              <div className="d-flex align-items-center gap-3">
                <span className="text-muted small d-none d-sm-inline">
                  {user?.name}
                </span>
                <div
                  className="d-flex align-items-center justify-content-center bg-primary text-white fw-bold rounded-circle border border-secondary"
                  style={{
                    width: "40px",
                    height: "40px",
                    fontSize: "0.9rem",
                    cursor: "default",
                  }}
                  title={`Logged in as ${user?.name}`}
                >
                  {getUserInitials(user?.name)}
                </div>
                <Button
                  variant="outline-light"
                  size="sm"
                  onClick={logout}
                  className="ms-2"
                >
                  Logout
                </Button>
              </div>
            </Then>
            <Else>
              {/* Unauthenticated State: Generic Icon with Redirect Trigger */}
              <div
                className="d-flex align-items-center justify-content-center bg-secondary text-light rounded-circle border border-dashed border-light cursor-pointer"
                style={{ width: "40px", height: "40px", cursor: "pointer" }}
                onClick={handleSsoRedirect}
                title="Click to sign in with Custom SSO"
              >
                <i className="bi bi-person-fill fs-5"></i>
              </div>
            </Else>
          </If>
        </Nav>
      </Container>
    </Navbar>
  );
};

export default TopNavigation;
