// src/features/auth/LoginCallback.jsx
import { useEffect, useContext, useState } from "react";
import { Container, Spinner, Alert } from "react-bootstrap";
import { If, Then, Else } from "react-if";
import { AuthContext } from "./AuthContext";

const LoginCallback = () => {
  const { setUser } = useContext(AuthContext);
  const [error, setError] = useState(null);

  useEffect(() => {
    const processToken = async () => {
      try {
        // Query parameters returned from your custom SSO redirect
        const urlParams = new URLSearchParams(window.location.search);
        const token = urlParams.get("token");

        if (!token) {
          throw new Error(
            "No authentication token found in callback URL query parameters.",
          );
        }

        // Commit token securely into local storage for the Axios interception client
        localStorage.setItem("sso_token", token);

        // Optional: Fetch formal profile details using the newly acquired token
        // const profileResponse = await axiosClient.get('/auth/me');
        // setUser(profileResponse.data.user);

        // Instant synchronous state commitment to cleanly pop route guards
        setUser({ id: "analyst_01", name: "Security Analyst" });

        // Clear tokens from visual URL space and return user back to index dashboard
        window.history.replaceState({}, document.title, window.location.origin);
      } catch (err) {
        console.error("SSO Token Ingestion Fail:", err);
        setError(
          err.message || "Failed to parse authentication exchange credentials.",
        );
      }
    };

    processToken();
  }, [setUser]);

  return (
    <Container
      className="d-flex vh-100 justify-content-center align-items-center bg-dark text-light"
      fluid
    >
      <div className="text-center" style={{ maxWidth: "450px" }}>
        <If condition={!!error}>
          <Then>
            <Alert variant="danger" className="text-start">
              <Alert.Heading>Authentication Error</Alert.Heading>
              <p className="mb-0 small">{error}</p>
              <hr />
              <div className="d-flex justify-content-end">
                <a href="/" className="btn btn-sm btn-outline-danger">
                  Return to Login
                </a>
              </div>
            </Alert>
          </Then>
          <Else>
            <Spinner animation="border" variant="primary" />
            <h5 className="mt-3 fw-semibold">Ingesting SSO Session Token</h5>
            <p className="text-muted small">
              Finalizing handshake and building security profile environment...
            </p>
          </Else>
        </If>
      </div>
    </Container>
  );
};

export default LoginCallback;
