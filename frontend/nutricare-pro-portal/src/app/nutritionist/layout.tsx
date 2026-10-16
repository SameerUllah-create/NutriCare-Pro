import type { ReactNode } from "react";
import { noIndexMetadata } from "../seo";
import { PortalShell } from "@/components/PortalShell";

export const metadata = noIndexMetadata("Nutritionist Dashboard");

export default function NutritionistLayout({ children }: { children: ReactNode }) {
  return <PortalShell roles={["Nutritionist"]}>{children}</PortalShell>;
}
