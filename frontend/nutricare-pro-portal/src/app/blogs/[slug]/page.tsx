"use client";
import { useParams } from "next/navigation";
import { MarketingShell } from "../../components/MarketingShell";
import { useResource } from "@/lib/useResource";
import { Notice } from "@/components/Forms";
import type { Content } from "@/types/clinical";
export default function Page() {const {slug}=useParams<{slug:string}>(),{data,error,loading}=useResource<Content>(`/api/blogs/${encodeURIComponent(slug)}`);return <MarketingShell><article className="marketing-section"><Notice error={error} loading={loading}/>{data&&<><h1>{data.title}</h1><p>{data.summary}</p><div style={{whiteSpace:"pre-wrap"}}>{data.content}</div></>}</article></MarketingShell>;}
