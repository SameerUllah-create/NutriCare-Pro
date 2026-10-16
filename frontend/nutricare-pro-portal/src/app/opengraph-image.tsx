import { ImageResponse } from "next/og";

export const runtime = "edge";
export const alt = "NutriCare Pro premium nutrition consultation platform";
export const size = {
  width: 1200,
  height: 630
};
export const contentType = "image/png";

export default function Image() {
  return new ImageResponse(
    (
      <div
        style={{
          alignItems: "center",
          background: "#f7f8f6",
          color: "#13231f",
          display: "flex",
          height: "100%",
          justifyContent: "center",
          padding: 64,
          width: "100%"
        }}
      >
        <div
          style={{
            background: "#ffffff",
            border: "2px solid #dfe5df",
            borderRadius: 28,
            display: "flex",
            flexDirection: "column",
            gap: 28,
            padding: 58,
            width: "100%"
          }}
        >
          <div style={{ color: "#1f7a68", fontSize: 30, fontWeight: 800 }}>NutriCare Pro</div>
          <div style={{ fontSize: 74, fontWeight: 900, lineHeight: 1.05 }}>
            Premium nutrition consultation platform
          </div>
          <div style={{ color: "#5e6f68", fontSize: 30, lineHeight: 1.4 }}>
            Appointments, diet plans, medical reports, progress tracking, chat, and video care.
          </div>
        </div>
      </div>
    ),
    size
  );
}
