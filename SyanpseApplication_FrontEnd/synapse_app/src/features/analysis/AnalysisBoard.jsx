import { useState } from "react";
import { useAuth } from "../../hooks/useAuth";
import { usePcapUpload } from "../../hooks/usePcapUpload";
import { useLlmStream } from "../../hooks/useLlmStream";
import { formatBytes } from "../../utils/fileFormatter";
import { Shield, File, Send, Trash2 } from "lucide-react";

import PcapDropzone from "../upload/PcapDropzone";
import ChatBubble from "./ChatBubble";

const AnalysisBoard = () => {
  const { user } = useAuth();
  const [file, setFile] = useState(null);
  const [prompt, setPrompt] = useState("");
  const [chatHistory, setChatHistory] = useState([]);

  const { uploadFile, isUploading, progress } = usePcapUpload();
  const { streamAnalysis, isStreaming } = useLlmStream();

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!prompt.trim() && !file) return;

    const currentPrompt = prompt;
    const attachedFile = file;

    const userMessage = {
      id: Date.now(),
      sender: "user",
      text: currentPrompt,
      attachedFile: attachedFile
        ? { name: attachedFile.name, size: formatBytes(attachedFile.size) }
        : null,
      timestamp: new Date().toLocaleTimeString(),
    };

    setChatHistory((prev) => [...prev, userMessage]);
    setPrompt("");
    setFile(null);

    const llmMessageId = Date.now() + 1;
    setChatHistory((prev) => [
      ...prev,
      {
        id: llmMessageId,
        sender: "llm",
        text: "",
        isThreat: undefined,
        timestamp: new Date().toLocaleTimeString(),
      },
    ]);

    let fileId = null;

    if (attachedFile) {
      const uploadRes = await uploadFile(attachedFile);
      fileId = uploadRes.fileId;
    }

    const updateLlmMessage = (text, isThreat) => {
      setChatHistory((prev) =>
        prev.map((msg) =>
          msg.id === llmMessageId ? { ...msg, text, isThreat } : msg,
        ),
      );
    };

    const isThreatDetected = await streamAnalysis(
      currentPrompt,
      fileId || "context-only",
      (chunk) => {
        updateLlmMessage(chunk, undefined);
      },
    );

    updateLlmMessage(
      (chatHistory.find((m) => m.id === llmMessageId)?.text || "") +
        "\n\nStream Ended.",
      isThreatDetected,
    );
  };

  if (!user) {
    return (
      <div className="flex-grow-1 d-flex flex-column justify-content-center align-items-center text-center p-5 text-light bg-dark">
        <Shield size={64} className="text-secondary mb-3" />
        <h2>Authentication Required</h2>
        <p className="text-muted">
          Please sign in via the SSO to access the PacketGuard Analysis Board.
        </p>
      </div>
    );
  }

  return (
    <div className="d-flex flex-column flex-grow-1 overflow-hidden">
      {/* Thread/Chat Workspace */}
      <div className="flex-grow-1 overflow-auto p-4 d-flex flex-column align-items-center">
        <div className="w-100" style={{ maxWidth: "800px" }}>
          {chatHistory.length === 0 ? (
            <div className="text-center my-5 py-5">
              <h1 className="display-5 fw-bold mb-3 text-primary">
                Network Threat Intelligence
              </h1>
              <p className="text-muted fs-5">
                Upload a packet capture and request automated analysis from your
                local model.
              </p>
            </div>
          ) : (
            chatHistory.map((msg) => (
              <ChatBubble key={msg.id} msg={msg} isStreaming={isStreaming} />
            ))
          )}
        </div>
      </div>

      {/* Input Area */}
      <div className="p-3 border-top border-secondary bg-dark d-flex justify-content-center">
        <form
          onSubmit={handleSubmit}
          className="w-100 position-relative"
          style={{ maxWidth: "800px" }}
        >
          {isUploading && (
            <div className="position-absolute top-0 start-0 w-100 h-100 bg-dark bg-opacity-75 d-flex flex-column justify-content-center align-items-center rounded z-3">
              <div
                className="spinner-border text-primary mb-2"
                role="status"
              ></div>
              <span className="small fw-bold">
                Uploading Capture... {progress}%
              </span>
            </div>
          )}

          {!file ? (
            <PcapDropzone onFileAccept={setFile} />
          ) : (
            <div className="d-flex align-items-center bg-secondary p-2 px-3 rounded-top border-bottom border-dark text-white justify-content-between">
              <div className="d-flex align-items-center small">
                <File size={16} className="me-2 text-primary" />
                <strong className="me-2">Target Capture:</strong> {file.name} (
                {formatBytes(file.size)})
              </div>
              <button
                type="button"
                className="btn btn-link text-white p-0"
                onClick={() => setFile(null)}
              >
                <Trash2 size={16} />
              </button>
            </div>
          )}

          <div className="input-group mt-1 shadow-sm">
            <textarea
              rows={2}
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              placeholder="Ask the local LLM to check logs, verify anomalies..."
              className="form-control bg-secondary border-0 text-white shadow-none"
              style={{ resize: "none" }}
              disabled={isUploading || isStreaming}
            />
            <button
              type="submit"
              className="btn btn-primary px-4 d-flex align-items-center gap-2"
              disabled={isUploading || isStreaming || (!prompt.trim() && !file)}
            >
              <Send size={18} />{" "}
              <span className="d-none d-md-inline">Analyze</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default AnalysisBoard;
