// Deterministic browser contract-test server. Never imported by the application.
import { createServer } from "node:http";
const user = { id: 1, email: "patient@example.test", firstName: "Test", lastName: "Patient", role: "Patient", isActive: true, isEmailVerified: true };
const token = Buffer.from(JSON.stringify({ alg: "none" })).toString("base64url") + "." + Buffer.from(JSON.stringify({ uid: "1", role: "Patient", exp: Math.floor(Date.now()/1000)+3600 })).toString("base64url") + ".test-only";
const server = createServer(async (request,response) => {
  response.setHeader("Content-Type","application/json");
  const path = new URL(request.url,"http://127.0.0.1").pathname.toLowerCase();
  const send=(data,status=200)=>{response.statusCode=status;response.end(JSON.stringify({success:status<400,data,...(status>=400?{message:"Test service rejected request"}:{})}));};
  if(path==="/health")return send({ready:true});
  if(path==="/api/auth/login"){let body="";for await(const chunk of request)body+=chunk;const values=JSON.parse(body);return values.password==="ValidPass123"?send({accessToken:token,refreshToken:"test-refresh-token",user}):send(null,401);}
  if(path==="/api/auth/refresh-token")return send({accessToken:token,refreshToken:"test-rotated-refresh-token"});
  if(path==="/api/auth/1")return request.headers.authorization?send(user):send(null,401);
  if(path==="/api/auth/logout")return send({loggedOut:true});
  if(path==="/api/services"||path.startsWith("/api/content/")||path==="/api/policies"||path==="/api/blogs")return send([]);
  if(path==="/api/patient-dashboard")return send(null,503);
  if(path==="/api/contact")return send({id:1,status:"Received"});
  return send(null,404);
});
server.listen(7111,"127.0.0.1");
process.on("SIGTERM",()=>server.close());
