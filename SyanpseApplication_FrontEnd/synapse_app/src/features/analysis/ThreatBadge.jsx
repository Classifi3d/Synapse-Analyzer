// src/features/analysis/ThreatBadge.jsx
import { Badge } from "react-bootstrap";
import { If, Then, Else } from "react-if";

const ThreatBadge = ({ isThreat }) => {
  return (
    <If condition={isThreat}>
      <Then>
        <Badge
          bg="danger"
          className="p-2 fw-semibold d-flex align-items-center gap-2"
          style={{ width: "fit-content" }}
        >
          <i className="bi bi-shield-x fs-6"></i> Critical Threat Detected
        </Badge>
      </Then>
      <Else>
        <Badge
          bg="success"
          className="p-2 fw-semibold d-flex align-items-center gap-2"
          style={{ width: "fit-content" }}
        >
          <i className="bi bi-shield-check fs-6"></i> Safe / No Anomalies
        </Badge>
      </Else>
    </If>
  );
};

export default ThreatBadge;
