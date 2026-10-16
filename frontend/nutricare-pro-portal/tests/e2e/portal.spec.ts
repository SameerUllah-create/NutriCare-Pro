import { test, expect } from "@playwright/test";
test("anonymous portal requires sign in without exposing token entry", async ({page})=>{
  await page.goto("/patient"); await expect(page.getByRole("heading",{name:"Sign in to continue"})).toBeVisible();
  await expect(page.getByText("Bearer token")).toHaveCount(0);
});
test("login uses HttpOnly cookies and never exposes credentials to browser storage", async ({page,context})=>{
  await page.goto("/login?next=%2F%5Cuntrusted.example"); await page.getByLabel("Email",{exact:true}).fill("patient@example.test");await page.getByLabel("Password",{exact:true}).fill("ValidPass123");
  await page.getByRole("button",{name:"Sign in",exact:true}).click();await expect(page).toHaveURL(/\/patient$/);
  const cookies=await context.cookies();expect(cookies.find(cookie=>cookie.name==="nc_access")?.httpOnly).toBe(true);expect(cookies.find(cookie=>cookie.name==="nc_refresh")?.sameSite).toBe("Strict");
  expect(await page.evaluate(()=>localStorage.getItem("accessToken"))).toBeNull();expect(await page.evaluate(()=>document.cookie)).not.toContain("nc_access");
});
test("account mutations reject a cross-origin request",async({request})=>{
  const response=await request.post("/api/auth/login",{headers:{origin:"https://untrusted.example","x-forwarded-host":"untrusted.example","x-forwarded-proto":"https"},data:{email:"patient@example.test",password:"ValidPass123"}});expect(response.status()).toBe(403);
});
test("Urdu switch sets RTL and localizes the core login form",async({page})=>{
  await page.goto("/login");await page.getByLabel("Language / زبان").selectOption("ur");await expect(page.locator("html")).toHaveAttribute("dir","rtl");await expect(page.getByRole("heading",{name:"خوش آمدید"})).toBeVisible();await expect(page.getByLabel("ای میل",{exact:true})).toBeVisible();
  await page.reload();await expect(page.locator("html")).toHaveAttribute("lang","ur");
});
test("public views show empty published catalog without draft services or invented testimonials",async({page})=>{
  await page.goto("/services");await expect(page.getByText("No published services are available yet.")).toBeVisible();
  await page.goto("/testimonials");await expect(page.getByText("Approved information has not been published yet.")).toBeVisible();await expect(page.locator('script[type="application/ld+json"]')).toHaveCount(0);
});

const patient={id:1,email:"patient@example.test",firstName:"Test",lastName:"Patient",role:"Patient",isActive:true,isEmailVerified:true};
test("Urdu booking records the displayed consent language and policy versions",async({page})=>{
  const consentBodies:Record<string,unknown>[]=[];
  await page.route("**/api/session",route=>route.fulfill({json:{success:true,data:patient}}));
  await page.route("**/api/proxy/api/**",route=>{
    const url=new URL(route.request().url());let data:unknown=[];
    if(url.pathname.endsWith("/services"))data=[{id:3,name:"Consultation",nameUrdu:"مشاورت",nutritionistId:12,durationMinutes:45,price:2500,currency:"PKR",mode:"Online"}];
    else if(url.pathname.endsWith("/policies"))data=[{id:1,purpose:"HealthData",version:"health-2",title:"Health consent",titleUrdu:"طبی رضامندی",content:"Approved health text",contentUrdu:"منظور شدہ طبی متن"},{id:2,purpose:"Teleconsultation",version:"video-3",title:"Video consent",titleUrdu:"ویڈیو رضامندی",content:"Approved video text",contentUrdu:"منظور شدہ ویڈیو متن"}];
    else if(url.pathname.includes("/availability/"))data=["2026-12-15T05:00:00Z"];
    else if(url.pathname.endsWith("/cancellation-policy"))data={fullRefundHours:24,freeRescheduleHours:6,maxFreeReschedules:1,version:1};
    else if(url.pathname.endsWith("/consents")){consentBodies.push(route.request().postDataJSON());data={id:consentBodies.length};}
    else if(url.pathname.endsWith("/intake"))data={id:8};
    return route.fulfill({json:{success:true,data}});
  });
  await page.goto("/book");await page.getByLabel("Language / زبان").selectOption("ur");await page.locator('select').filter({has:page.locator('option[value="3"]')}).selectOption("3");await page.getByRole("button",{name:"اگلا",exact:true}).click();
  await page.locator('input[type="date"]').fill("2026-12-15");await page.getByRole("radio").check();await page.getByRole("button",{name:"اگلا",exact:true}).click();
  await expect(page.getByRole("heading",{name:"طبی رضامندی"})).toBeVisible();await page.locator('textarea[name="goals"]').fill("Improve nutrition");await page.locator('input[name="healthConsent"]').check();await page.locator('input[name="teleConsent"]').check();await page.getByRole("button",{name:"اگلا",exact:true}).click();
  await expect(page.getByRole("heading",{name:"جائزہ",exact:true})).toBeVisible();expect(consentBodies).toEqual([{purpose:"HealthData",policyVersion:"health-2",language:"ur",accepted:true},{purpose:"Teleconsultation",policyVersion:"video-3",language:"ur",accepted:true}]);
});

test("appointment history retains the booked policy and legacy null quotes cannot start checkout",async({page})=>{
  let checkouts=0;
  const appointment={id:41,patientId:1,nutritionistId:12,appointmentDateTime:"2026-12-15T05:00:00Z",duration:45,status:"Confirmed",appointmentType:"Video",priceSnapshot:null,currency:"PKR",cancellationPolicySnapshotJson:JSON.stringify({FullRefundHours:48,FreeRescheduleHours:12,MaxFreeReschedules:2,Version:4})};
  await page.route("**/api/session",route=>route.fulfill({json:{success:true,data:patient}}));
  await page.route("**/api/proxy/api/**",route=>{
    const path=new URL(route.request().url()).pathname.toLowerCase();
    if(path.endsWith("/process"))checkouts++;
    return route.fulfill({json:{success:true,data:path.endsWith("/appointments/history")?[appointment]:[]}});
  });
  await page.goto("/appointments");await page.getByRole("button",{name:"View details"}).click();await expect(page.getByRole("heading",{name:"Cancellation policy · Version 4"})).toBeVisible();await expect(page.locator(".policy-summary dd").first()).toHaveText("48");
  await page.goto("/payments?appointment=41");await expect(page.getByText("This older booking has no payable quote. Contact support.")).toBeVisible();await expect(page.getByRole("button",{name:"Continue to checkout"})).toBeDisabled();expect(checkouts).toBe(0);
});

test("patient file export downloads through the same-origin proxy and shows assisted-export conflicts",async({page})=>{
  let exports=0;
  await page.route("**/api/session",route=>route.fulfill({json:{success:true,data:patient}}));
  await page.route("**/api/proxy/api/**",route=>{
    const url=new URL(route.request().url());
    if(!url.pathname.endsWith("/privacy/export/bundle"))return route.fulfill({json:{success:true,data:[]}});
    expect(url.pathname).toBe("/api/proxy/api/privacy/export/bundle");expect(url.search).toBe("");expect(route.request().method()).toBe("GET");exports++;
    return exports===1?route.fulfill({contentType:"application/zip",body:Buffer.from("UEsFBgAAAAAAAAAAAAAAAAAAAAAAAA==","base64")}):route.fulfill({status:409,json:{success:false,message:"Some files need an assisted export. Please contact support."}});
  });
  await page.goto("/privacy");
  const button=page.getByRole("button",{name:"Export records and files (ZIP)",exact:true});
  const [file]=await Promise.all([page.waitForEvent("download"),button.click()]);expect(file.suggestedFilename()).toBe("nutricare-records.zip");expect(await file.failure()).toBeNull();
  await expect(button).toBeEnabled();await button.click();await expect(page.getByRole("main").getByRole("alert")).toHaveText("Some files need an assisted export. Please contact support.");expect(exports).toBe(2);
});
