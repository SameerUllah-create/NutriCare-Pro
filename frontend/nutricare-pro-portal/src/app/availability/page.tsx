import { PortalShell } from "@/components/PortalShell";
import { ConfigurationManager } from "@/components/ConfigurationManager";
export default function Page(){return <PortalShell roles={["Admin","Nutritionist"]}><ConfigurationManager initialKind="availability"/></PortalShell>;}
