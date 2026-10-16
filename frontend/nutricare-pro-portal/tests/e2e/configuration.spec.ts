import {test,expect} from "@playwright/test";
import type {Page} from "@playwright/test";
const admin={id:9,email:"staff@example.test",firstName:"Test",lastName:"Admin",role:"Admin",isActive:true,isEmailVerified:true};
const clinician={id:12,name:"Verified Clinician",isVerified:true,isActive:true,isEmailVerified:true};
const draft={id:1,name:"Draft initial consultation",nameUrdu:"ابتدائی مشاورت",description:"Pending approval",descriptionUrdu:"منظوری کا منتظر",durationMinutes:45,mode:"Online",price:2500,currency:"PKR",bookingBufferMinutes:15,isPublished:false,isActive:false,version:1,rowVersion:"AAAAAAAB"};
test("configuration requires explicit approval and saves drafts without activating them",async({page})=>{
  let entity={...draft},publishes=0,saves=0;
  await page.route("**/api/session",route=>route.fulfill({json:{success:true,data:admin}}));
  await page.route("**/api/proxy/api/configuration**",async route=>{
    const url=new URL(route.request().url());
    if(route.request().method()==="GET")return route.fulfill({json:{success:true,data:{services:[entity],packages:[],availabilityRules:[],exceptions:[],cancellationPolicies:[],policyDocuments:[],history:[],clinicians:[clinician,{...clinician,id:13,name:"Unverified Clinician",isVerified:false}]}}});
    if(url.pathname.endsWith("/publish")){publishes++;expect(route.request().postDataJSON().approvalConfirmed).toBe(true);entity={...entity,isPublished:true,isActive:true};return route.fulfill({json:{success:true,data:entity}});}
    saves++;const body=route.request().postDataJSON();expect(body.isPublished).toBeUndefined();expect(body.isActive).toBeUndefined();expect(body.expectedVersion).toBe(1);expect(body.nutritionistId).toBe(12);expect(body.currency).toBe("PKR");entity={...entity,...body,version:2,rowVersion:"AAAAAAAC"};return route.fulfill({json:{success:true,data:entity}});
  });
  await page.goto("/configuration");await page.getByRole("button",{name:"Edit",exact:true}).click();await expect(page.getByRole("button",{name:"Publish",exact:true})).toBeDisabled();
  await expect(page.getByRole("combobox",{name:"Clinician",exact:true})).toHaveValue("");await expect(page.getByRole("option",{name:"Unverified Clinician",exact:true})).toHaveCount(0);await page.getByRole("combobox",{name:"Clinician",exact:true}).selectOption("12");await page.getByLabel("Price",{exact:true}).fill("2600");await page.getByRole("button",{name:"Save draft",exact:true}).click();await expect(page.getByRole("status")).toContainText("Draft saved");expect(saves).toBe(1);expect(publishes).toBe(0);
  await page.getByRole("checkbox",{name:/I confirm/}).check();await page.getByRole("button",{name:"Publish",exact:true}).click();await expect(page.getByRole("status")).toContainText("explicitly published");expect(publishes).toBe(1);
});
test("version conflicts preserve form edits and block publication until reloaded",async({page})=>{
  await page.route("**/api/session",route=>route.fulfill({json:{success:true,data:{...admin,role:"Nutritionist"}}}));
  await page.route("**/api/proxy/api/configuration**",route=>route.request().method()==="GET"?route.fulfill({json:{success:true,data:{services:[draft],packages:[],availabilityRules:[],exceptions:[],cancellationPolicies:[],policyDocuments:[],history:[]}}}):route.fulfill({status:409,json:{success:false,message:"The configuration changed. Reload before saving."}}));
  await page.goto("/configuration");await page.getByRole("button",{name:"Edit",exact:true}).click();await page.getByLabel("Price",{exact:true}).fill("2800");await page.getByRole("button",{name:"Save draft",exact:true}).click();await expect(page.getByLabel("Price",{exact:true})).toHaveValue("2800");await expect(page.getByRole("button",{name:"Reload latest version"})).toBeVisible();await expect(page.getByRole("button",{name:"Publish",exact:true})).toBeDisabled();
});

test("working-hour drafts send an explicit clinician and SQL TimeOnly values",async({page})=>{
  let saved=false;
  await page.route("**/api/session",route=>route.fulfill({json:{success:true,data:admin}}));
  await page.route("**/api/proxy/api/configuration**",route=>{
    if(route.request().method()==="GET")return route.fulfill({json:{success:true,data:{services:[],packages:[],availabilityRules:[],exceptions:[],cancellationPolicies:[],policyDocuments:[],history:[],clinicians:[clinician]}}});
    const body=route.request().postDataJSON();expect(body.nutritionistId).toBe(12);expect(body.startTime).toBe("10:00:00");expect(body.endTime).toBe("18:00:00");expect(body.dayOfWeek).toBe(1);saved=true;
    return route.fulfill({json:{success:true,data:{...body,id:2,isPublished:false,version:1,rowVersion:"AAAAAAAC"}}});
  });
  await page.goto("/configuration");await page.getByRole("button",{name:"Working hours",exact:true}).click();
  await page.getByRole("combobox",{name:"Clinician",exact:true}).selectOption("12");await page.getByLabel("Day of week").selectOption("1");await page.getByLabel("Start time").fill("10:00");await page.getByLabel("End time").fill("18:00");await page.getByLabel("Booking buffer (minutes)").fill("15");
  await page.getByRole("button",{name:"Save draft",exact:true}).click();await expect(page.getByRole("status")).toContainText("Draft saved");expect(saved).toBe(true);
});

test("server field validation under data.errors is visible without discarding draft edits",async({page})=>{
  await page.route("**/api/session",route=>route.fulfill({json:{success:true,data:{...admin,role:"Nutritionist"}}}));
  await page.route("**/api/proxy/api/configuration**",route=>route.request().method()==="GET"?route.fulfill({json:{success:true,data:{services:[draft],packages:[],availabilityRules:[],exceptions:[],cancellationPolicies:[],policyDocuments:[],history:[]}}}):route.fulfill({status:400,json:{success:false,message:"Validation failed",data:{errors:{price:["Use a positive PKR amount with at most two decimal places"]}}}}));
  await page.goto("/configuration");await page.getByRole("button",{name:"Edit",exact:true}).click();await page.getByLabel("Price",{exact:true}).fill("2800");await page.getByRole("button",{name:"Save draft",exact:true}).click();
  await expect(page.getByRole("main").getByRole("alert")).toContainText("price: Use a positive PKR amount with at most two decimal places");await expect(page.getByLabel("Price",{exact:true})).toHaveValue("2800");
});

test("published configuration can be managed and unpublished after a page reload",async({page})=>{
  let entity={...draft,nutritionistId:12,effectiveFrom:"2026-10-01T00:00:00Z"},publishes=0,unpublishes=0,revisions=0;
  const history:{id:number;entityType:string;entityId:number;version:number;action:string;changedByUserId:number;createdAt:string}[]=[];
  await page.route("**/api/session",route=>route.fulfill({json:{success:true,data:admin}}));
  await page.route("**/api/proxy/api/configuration**",route=>{
    const url=new URL(route.request().url());
    if(route.request().method()==="GET")return route.fulfill({json:{success:true,data:{services:[entity],packages:[],availabilityRules:[],exceptions:[],cancellationPolicies:[],policyDocuments:[],history,clinicians:[clinician]}}});
    const body=route.request().postDataJSON();
    if(url.pathname.endsWith("/publish")){expect(body.approvalConfirmed).toBe(true);publishes++;entity={...entity,isPublished:true,isActive:true,version:2,rowVersion:"AAAAAAAC"};return route.fulfill({json:{success:true,data:entity}});}
    if(url.pathname.endsWith("/unpublish")){
      expect(body).toEqual({expectedVersion:2,rowVersion:"AAAAAAAC",reason:"Replace the proposed price after review"});unpublishes++;entity={...entity,isPublished:false,isActive:false,version:3,rowVersion:"AAAAAAAD"};
      history.push({id:8,entityType:"services",entityId:1,version:3,action:"Unpublished: Replace the proposed price after review",changedByUserId:9,createdAt:"2026-10-16T08:00:00Z"});return route.fulfill({json:{success:true,data:entity}});
    }
    revisions++;return route.fulfill({status:400,json:{success:false,message:"Unexpected revision or draft update"}});
  });
  await page.goto("/configuration");await page.getByRole("button",{name:"Edit",exact:true}).click();await page.getByRole("checkbox",{name:/I confirm/}).check();await page.getByRole("button",{name:"Publish",exact:true}).click();await expect(page.getByRole("status")).toContainText("explicitly published");
  await page.reload();await page.getByRole("button",{name:/View \/ manage Draft initial consultation/}).click();await expect(page.getByRole("button",{name:"Create revision",exact:true})).toBeVisible();await expect(page.getByRole("heading",{name:"Published configuration",exact:true})).toBeVisible();
  await expect(page.getByLabel("Price",{exact:true})).toBeDisabled();await expect(page.getByRole("button",{name:"Save draft",exact:true})).toBeDisabled();await expect(page.getByRole("button",{name:"Unpublish",exact:true})).toBeDisabled();
  await page.getByLabel("Reason",{exact:true}).fill("Replace the proposed price after review");await page.getByRole("button",{name:"Unpublish",exact:true}).click();await expect(page.getByRole("status")).toContainText("Configuration unpublished");await expect(page.getByRole("cell",{name:"Unpublished: Replace the proposed price after review",exact:true})).toBeVisible();
  expect(publishes).toBe(1);expect(unpublishes).toBe(1);expect(revisions).toBe(0);
});

const packageDraft={id:21,name:"Draft monthly package",nameUrdu:"مجوزہ ماہانہ پیکیج",durationDays:28,durationWeeks:4,initialConsultationCount:1,initialConsultationMinutes:45,followUpConsultationCount:3,followUpConsultationMinutes:20,dietPlanCount:1,dietPlanRevisionCount:2,progressTrackingFrequency:"Weekly",creditExpiryDays:28,firstConsultationWithinDays:14,eligibleServiceCodes:["GeneralNutrition"],activationRule:"FirstCompletedConsultation",noShowCreditRule:"CancellationPolicy",restoreCreditsOnProviderCancellation:true,price:7500,currency:"PKR",effectiveFrom:"2026-10-01T00:00:00Z",isPublished:false,isActive:false,version:1,rowVersion:"AAAAAAAB"};
type PackageFixture=typeof packageDraft&{nutritionistApprovedById:number|null;nutritionistApprovedAt:string|null;nutritionistApprovedVersion:number|null};
const unreviewedPackage=():PackageFixture=>({...packageDraft,nutritionistApprovedById:null,nutritionistApprovedAt:null,nutritionistApprovedVersion:null});
const packageConfiguration=(packages:PackageFixture[])=>({services:[],packages,availabilityRules:[],exceptions:[],cancellationPolicies:[],policyDocuments:[],history:[],clinicians:[{...clinician,userId:42}]});
async function emptyEnrollments(page:Page){await page.route("**/api/proxy/api/package-enrollments**",route=>route.fulfill({json:{success:true,data:{items:[],total:0,page:1,pageSize:50,checkoutEnabled:false}}}));}
async function selectPackage(page:Page){await page.goto("/configuration");await page.getByRole("button",{name:"Packages",exact:true}).click();await expect(page.getByText(/Package #21 .*Duration \(days\): 28 .*Initial consultations included: 1 .*Follow-up consultations included: 3/)).toBeVisible();await page.getByRole("button",{name:"Edit",exact:true}).click();}

test("package drafts serialize entitlement arrays and invalidate review without publishing",async({page})=>{
  let entity:PackageFixture={...unreviewedPackage(),nutritionistApprovedById:42,nutritionistApprovedAt:"2026-10-16T06:00:00Z",nutritionistApprovedVersion:1},saves=0,publishes=0;
  await page.route("**/api/session",route=>route.fulfill({json:{success:true,data:admin}}));await emptyEnrollments(page);
  await page.route("**/api/proxy/api/configuration**",route=>{
    if(route.request().method()==="GET")return route.fulfill({json:{success:true,data:packageConfiguration([entity])}});
    if(route.request().url().endsWith("/publish")){publishes++;return route.fulfill({status:400,json:{success:false,message:"Unexpected publication"}});}
    const body=route.request().postDataJSON();saves++;expect(body.durationDays).toBe(30);expect(body.durationWeeks).toBeUndefined();expect(body.initialConsultationCount).toBe(1);expect(body.initialConsultationMinutes).toBe(45);expect(body.followUpConsultationCount).toBe(4);expect(body.followUpConsultationMinutes).toBe(20);expect(body.dietPlanCount).toBe(1);expect(body.dietPlanRevisionCount).toBe(2);expect(body.creditExpiryDays).toBe(28);expect(body.firstConsultationWithinDays).toBe(14);expect(body.progressTrackingFrequency).toBe("Weekly");expect(body.eligibleServiceCodes).toEqual(["GeneralNutrition","WeightLoss","Diabetes"]);expect(body.activationRule).toBe("FirstCompletedConsultation");expect(body.noShowCreditRule).toBe("CancellationPolicy");expect(body.restoreCreditsOnProviderCancellation).toBe(true);expect(body.nutritionistApprovedById).toBeUndefined();expect(body.nutritionistApprovedVersion).toBeUndefined();expect(body.isPublished).toBeUndefined();expect(body.isActive).toBeUndefined();
    entity={...entity,...body,durationWeeks:5,version:2,rowVersion:"AAAAAAAC",nutritionistApprovedById:null,nutritionistApprovedAt:null,nutritionistApprovedVersion:null};return route.fulfill({json:{success:true,data:entity}});
  });
  await selectPackage(page);await expect(page.getByRole("button",{name:"Record nutritionist review"})).toHaveCount(0);await page.getByRole("checkbox",{name:/I confirm/}).check();await expect(page.getByRole("button",{name:"Publish",exact:true})).toBeEnabled();
  await page.getByLabel("Duration (days)",{exact:true}).fill("30");await page.getByLabel("Follow-up consultations included",{exact:true}).fill("4");await page.getByRole("checkbox",{name:"Weight loss",exact:true}).check();await page.getByRole("checkbox",{name:"Diabetes nutrition",exact:true}).check();await expect(page.getByRole("button",{name:"Publish",exact:true})).toBeDisabled();
  await page.getByRole("button",{name:"Save draft",exact:true}).click();await expect(page.getByRole("status").filter({hasText:"Draft saved"})).toBeVisible();await expect(page.getByText("Draft / awaiting nutritionist review; checkout disabled",{exact:true})).toBeVisible();await page.getByRole("checkbox",{name:/I confirm/}).check();await expect(page.getByRole("button",{name:"Publish",exact:true})).toBeDisabled();expect(saves).toBe(1);expect(publishes).toBe(0);
});

test("nutritionist package review is explicit, preserves quantities and requires separate publication",async({page})=>{
  let entity=unreviewedPackage(),records=[entity],reviews=0,publishes=0,billingReads=0;
  await page.route("**/api/session",route=>route.fulfill({json:{success:true,data:{...admin,id:42,role:"Nutritionist"}}}));
  await page.route("**/api/proxy/api/package-enrollments**",route=>{billingReads++;return route.fulfill({status:403,json:{success:false,message:"Billing is restricted"}});});
  await page.route("**/api/proxy/api/configuration**",route=>{
    const url=new URL(route.request().url());if(route.request().method()==="GET")return route.fulfill({json:{success:true,data:packageConfiguration(records)}});
    if(url.pathname.endsWith("/revision")){const revision={...entity,id:22,version:2,rowVersion:"AAAAAAAE",isPublished:false,isActive:false,nutritionistApprovedById:null,nutritionistApprovedAt:null,nutritionistApprovedVersion:null};records=[entity,revision];return route.fulfill({json:{success:true,data:revision}});}
    const body=route.request().postDataJSON();
    if(url.pathname.endsWith("/nutritionist-approval")){expect(body).toEqual({expectedVersion:1,rowVersion:"AAAAAAAB",approvalConfirmed:true});reviews++;entity={...entity,rowVersion:"AAAAAAAC",nutritionistApprovedById:42,nutritionistApprovedAt:"2026-10-16T06:00:00Z",nutritionistApprovedVersion:1};records=[entity];return route.fulfill({json:{success:true,data:entity}});}
    expect(url.pathname).toMatch(/\/publish$/);expect(body).toEqual({expectedVersion:1,rowVersion:"AAAAAAAC",approvalConfirmed:true});publishes++;entity={...entity,isPublished:true,isActive:true,rowVersion:"AAAAAAAD"};records=[entity];return route.fulfill({json:{success:true,data:entity}});
  });
  await selectPackage(page);const review=page.getByRole("button",{name:"Record nutritionist review",exact:true});await expect(review).toBeDisabled();await page.getByRole("checkbox",{name:/I confirm/}).check();await expect(page.getByRole("button",{name:"Publish",exact:true})).toBeDisabled();
  await page.getByRole("checkbox",{name:/I have reviewed all package/}).check();await review.click();await expect(page.getByRole("status").filter({hasText:"Nutritionist review recorded"})).toBeVisible();expect(reviews).toBe(1);expect(publishes).toBe(0);expect(entity.isPublished).toBe(false);expect(entity.initialConsultationCount).toBe(1);expect(entity.followUpConsultationCount).toBe(3);expect(entity.version).toBe(1);await expect(page.getByRole("button",{name:"Publish",exact:true})).toBeDisabled();
  await page.getByRole("checkbox",{name:/I confirm/}).check();await page.getByRole("button",{name:"Publish",exact:true}).click();await expect(page.getByText("Package published; checkout disabled",{exact:true})).toBeVisible();await page.getByRole("button",{name:"Create revision",exact:true}).click();await expect(page.getByText("Draft / awaiting nutritionist review; checkout disabled",{exact:true})).toBeVisible();await expect(page.getByRole("button",{name:"Publish",exact:true})).toBeDisabled();expect(records[0].isPublished).toBe(true);expect(records[1].isPublished).toBe(false);expect(records[1].initialConsultationCount).toBe(1);expect(records[1].dietPlanRevisionCount).toBe(2);expect(records[1].nutritionistApprovedVersion).toBeNull();expect(publishes).toBe(1);expect(billingReads).toBe(0);
});

test("stale nutritionist review cannot approve or publish a changed package version",async({page})=>{
  let entity:PackageFixture={...unreviewedPackage(),nutritionistApprovedById:42,nutritionistApprovedAt:"2026-10-16T06:00:00Z",nutritionistApprovedVersion:0},reviews=0,publishes=0;
  await page.route("**/api/session",route=>route.fulfill({json:{success:true,data:{...admin,id:42,role:"Nutritionist"}}}));
  await page.route("**/api/proxy/api/configuration**",route=>{
    if(route.request().method()==="GET")return route.fulfill({json:{success:true,data:packageConfiguration([entity])}});
    if(route.request().url().endsWith("/publish")){publishes++;return route.fulfill({status:409,json:{success:false,message:"Review the current package version"}});}
    expect(route.request().postDataJSON().rowVersion).toBe("AAAAAAAB");reviews++;entity={...entity,version:2,rowVersion:"AAAAAAAC",nutritionistApprovedVersion:1};return route.fulfill({status:409,json:{success:false,message:"The package changed. Reload before review."}});
  });
  await selectPackage(page);await page.getByRole("checkbox",{name:/I confirm/}).check();await expect(page.getByRole("button",{name:"Publish",exact:true})).toBeDisabled();await page.getByRole("checkbox",{name:/I have reviewed all package/}).check();await page.getByRole("button",{name:"Record nutritionist review",exact:true}).click();await expect(page.getByRole("button",{name:"Reload latest version",exact:true})).toBeVisible();await expect(page.getByRole("button",{name:"Record nutritionist review",exact:true})).toBeDisabled();
  await page.getByRole("button",{name:"Reload latest version",exact:true}).click();await page.getByRole("checkbox",{name:/I confirm/}).check();await expect(page.getByRole("button",{name:"Publish",exact:true})).toBeDisabled();await expect(page.getByLabel("Initial consultations included",{exact:true})).toHaveValue("1");expect(reviews).toBe(1);expect(publishes).toBe(0);
});

test("Admin validity extensions require a reason and reload a stale enrollment before retry",async({page})=>{
  let enrollment={id:71,servicePackageId:21,patientId:6,packageVersion:1,purchasedAtUtc:"2026-10-01T05:00:00Z",firstConsultationDueAtUtc:"2026-10-15T05:00:00Z",activatedAtUtc:"2026-10-02T05:00:00Z",expiresAtUtc:"2026-10-30T05:00:00Z",status:"Active",rowVersion:"AAAAAAAB"},extensions=0;
  const history:{id:number;action:string;reason:string;actorUserId:number;previousExpiryUtc:string;newExpiryUtc:string;createdAt:string}[]=[];
  await page.route("**/api/session",route=>route.fulfill({json:{success:true,data:admin}}));
  await page.route("**/api/proxy/api/configuration**",route=>route.fulfill({json:{success:true,data:packageConfiguration([unreviewedPackage()])}}));
  await page.route("**/api/proxy/api/package-enrollments**",route=>{
    const url=new URL(route.request().url());
    if(route.request().method()==="GET")return route.fulfill({json:{success:true,data:url.pathname.endsWith("/71")?{enrollment,usage:[],history,checkoutEnabled:false}:{items:[enrollment],total:1,page:1,pageSize:50,checkoutEnabled:false}}});
    expect(url.pathname).toBe("/api/proxy/api/package-enrollments/71/extend");const body=route.request().postDataJSON();extensions++;expect(body.days).toBe(7);expect(body.reason).toBe("Approved recovery extension");
    if(extensions===1){expect(body.rowVersion).toBe("AAAAAAAB");enrollment={...enrollment,expiresAtUtc:"2026-10-31T05:00:00Z",rowVersion:"AAAAAAAC"};return route.fulfill({status:409,json:{success:false,message:"Enrollment changed; reload its latest version"}});}
    expect(body.rowVersion).toBe("AAAAAAAC");const previous=enrollment.expiresAtUtc;enrollment={...enrollment,expiresAtUtc:"2026-11-07T05:00:00Z",rowVersion:"AAAAAAAD"};history.push({id:1,action:"AdminExtendedValidity",reason:body.reason,actorUserId:9,previousExpiryUtc:previous,newExpiryUtc:enrollment.expiresAtUtc,createdAt:"2026-10-16T07:00:00Z"});return route.fulfill({json:{success:true,data:enrollment}});
  });
  await page.goto("/configuration");await page.getByRole("button",{name:"Packages",exact:true}).click();await page.getByRole("button",{name:"Manage validity #71",exact:true}).click();const submit=page.getByRole("button",{name:"Extend package validity",exact:true});await expect(submit).toBeDisabled();await page.getByLabel("Extend by (days)").fill("7");await expect(submit).toBeDisabled();await page.getByLabel("Extension reason").fill("Approved recovery extension");await submit.click();await expect(page.getByRole("button",{name:"Reload latest enrollment"})).toBeVisible();await expect(page.getByLabel("Extension reason")).toHaveValue("Approved recovery extension");await expect(submit).toBeDisabled();
  await page.getByRole("button",{name:"Reload latest enrollment"}).click();await expect(submit).toBeEnabled();await submit.click();await expect(page.getByText("Package validity extended. The audit history records the reason and previous expiry.",{exact:true})).toBeVisible();await expect(page.getByText("Approved recovery extension",{exact:true})).toBeVisible();await expect(page.getByText("Manage validity for existing paid enrollments. Package checkout remains disabled.",{exact:true})).toBeVisible();expect(extensions).toBe(2);
});
