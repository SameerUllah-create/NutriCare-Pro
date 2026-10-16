import type { ReactNode } from "react";
import { noIndexMetadata } from "../seo";
import { PortalShell } from "@/components/PortalShell";

export const metadata = noIndexMetadata("Patient Dashboard");

export default function PatientLayout({ children }: { children: ReactNode }) {
  return <PortalShell roles={["Patient"]}>{children}</PortalShell>;
}
