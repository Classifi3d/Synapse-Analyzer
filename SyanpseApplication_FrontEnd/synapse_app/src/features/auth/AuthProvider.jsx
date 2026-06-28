// import { useState, useEffect } from "react";
// import { Spinner, Container, Row, Col } from "react-bootstrap";
// import { AuthContext } from "./AuthContext";
// import LoginCallback from "./LoginCallback";

// export const AuthProvider = ({ children }) => {
//   const [user, setUser] = useState(null);
//   const [loading, setLoading] = useState(true);

//   const isCallbackRoute = window.location.pathname === "/auth/callback";

//   const logout = () => {
//     localStorage.removeItem("sso_token");
//     setUser(null);
//     window.location.href = `${import.meta.env.VITE_SSO_LOGIN_URL}?redirect=${window.location.origin}`;
//   };

//   useEffect(() => {
//     const checkAuth = async () => {
//       if (isCallbackRoute) {
//         setLoading(false);
//         return;
//       }

//       const token = localStorage.getItem("sso_token");
//       if (!token) {
//         setLoading(false);
//         return;
//       }

//       try {
//         setUser({ id: "analyst_01", name: "Security Analyst" });
//       } catch {
//         localStorage.removeItem("sso_token");
//       } finally {
//         setLoading(false);
//       }
//     };

//     checkAuth();
//   }, [isCallbackRoute]);

//   if (loading) {
//     return (
//       <Container
//         className="d-flex vh-100 justify-content-center align-items-center bg-dark text-light"
//         fluid
//       >
//         <Row>
//           <Col className="text-center">
//             <Spinner animation="border" variant="primary" />
//             <p className="mt-3 text-muted">Initializing Guard Session...</p>
//           </Col>
//         </Row>
//       </Container>
//     );
//   }

//   if (isCallbackRoute && !user) {
//     return (
//       <AuthContext.Provider value={{ user, setUser, logout }}>
//         <LoginCallback />
//       </AuthContext.Provider>
//     );
//   }

//   return (
//     <AuthContext.Provider value={{ user, setUser, logout }}>
//       {children}
//     </AuthContext.Provider>
//   );
// };

import { useState } from "react";
import { AuthContext } from "./AuthContext";

export const AuthProvider = ({ children }) => {
  // 1. Immediately initialize with a dummy user instead of null
  const [user, setUser] = useState({
    id: "dev_analyst",
    name: "Guest Analyst (Dev Mode)",
  });

  // 2. Set loading directly to false so we don't get stuck on the spinner
  const [loading] = useState(false);

  const logout = () => {
    // Simply clear the mock state on logout
    setUser(null);
  };

  // 3. Directly return the provider, bypassing the callback route checks
  return (
    <AuthContext.Provider value={{ user, setUser, logout }}>
      {children}
    </AuthContext.Provider>
  );
};
