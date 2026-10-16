import type { ReactNode } from "react";
import { noIndexMetadata } from "../seo";
import { PortalShell } from "@/components/PortalShell";

export const metadata = noIndexMetadata("Admin Dashboard");

export default function AdminLayout({ children }: { children: ReactNode }) {
  return <PortalShell roles={["Admin"]}>{children}</PortalShell>;
}
