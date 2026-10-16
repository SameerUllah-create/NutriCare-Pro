import * as React from "react";
import { cn } from "@/lib/utils";
const Input = React.forwardRef<HTMLInputElement, React.ComponentProps<"input">>(({className, ...props}, ref) => <input className={cn("flex w-full rounded-lg border border-solid border-border bg-white px-3 py-2 text-base focus-visible:outline focus-visible:outline-2 disabled:opacity-50", className)} ref={ref} {...props} />);
Input.displayName = "Input";
export { Input };
