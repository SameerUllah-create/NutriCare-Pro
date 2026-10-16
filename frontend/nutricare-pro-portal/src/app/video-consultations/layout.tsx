import type { ReactNode } from "react";
import { noIndexMetadata } from "../seo";
import { PortalShell } from "@/components/PortalShell";

export const metadata = noIndexMetadata("Video Consultations");

export default function VideoConsultationsLayout({ children }: { children: ReactNode }) {
  return <PortalShell roles={["Patient", "Nutritionist"]}>{children}</PortalShell>;
}
