import { Card } from "react-bootstrap";
import { File } from "lucide-react";
import ThreatBadge from "./ThreatBadge";

const ChatBubble = ({ msg, isStreaming }) => {
  const isUser = msg.sender === "user";

  return (
    <div
      className={`mb-4 d-flex flex-column ${isUser ? "align-items-end" : "align-items-start"}`}
    >
      <div className="text-muted small mb-1">
        {isUser ? "You" : "Local LLM"} • {msg.timestamp}
      </div>
      <Card
        className={`p-3 border-0 ${isUser ? "bg-secondary text-white" : "bg-light text-dark"}`}
        style={{ maxWidth: "85%" }}
      >
        {msg.attachedFile && (
          <div className="p-2 mb-2 rounded bg-dark text-light small d-flex align-items-center">
            <File size={16} className="me-2 text-primary" />
            <span className="me-2">{msg.attachedFile.name}</span>
            <span className="text-muted">({msg.attachedFile.size})</span>
          </div>
        )}

        <p className="mb-0" style={{ whiteSpace: "pre-wrap" }}>
          {msg.text || (isStreaming && !isUser ? "Analyzing..." : "")}
        </p>

        {msg.isThreat !== undefined && (
          <div className="mt-3 border-top pt-2">
            <ThreatBadge isThreat={msg.isThreat} />
          </div>
        )}
      </Card>
    </div>
  );
};

export default ChatBubble;
