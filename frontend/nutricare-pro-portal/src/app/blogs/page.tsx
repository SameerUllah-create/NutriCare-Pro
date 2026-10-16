"use client";
import Link from "next/link";
import { MarketingShell } from "../components/MarketingShell";
import { useResource } from "@/lib/useResource";
import { Notice,Empty } from "@/components/Forms";
import { useLocale } from "@/components/LocaleProvider";
import type { Content } from "@/types/clinical";
export default function Page(){const {data,error,loading}=useResource<Content[]>("/api/blogs"),{t}=useLocale();return <MarketingShell><section className="marketing-section"><h1>{t("Blogs")}</h1><Notice error={error} loading={loading}/>{data?.length===0&&<Empty/>}<div className="service-grid">{data?.map(post=><article key={post.id} className="marketing-card"><h2><Link href={`/blogs/${post.slug}`}>{post.title}</Link></h2><p>{post.summary}</p></article>)}</div></section></MarketingShell>;}
