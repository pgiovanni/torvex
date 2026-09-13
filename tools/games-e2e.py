"""Games hub API smoke test (docs/GAMES-HUB.md) — DEV ONLY.

Runs against a LOCAL instance on http://127.0.0.1:5077 using two local users.
Mints dev JWTs from the API project's user-secrets (Jwt:Key/Issuer/Audience):
    py tools/games-e2e.py <UserSecretsId from peeposredemption.API.csproj>
Start the app first:  cd publish && ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5077 dotnet peeposredemption.API.dll
Every line prints PASS/FAIL. 20 checks as of 2026-09-12.
"""
import json,os,sys,hmac,hashlib,base64,time,urllib.request,urllib.error
sid=sys.argv[1]; d=json.load(open(os.path.join(os.environ["APPDATA"],"Microsoft","UserSecrets",sid,"secrets.json"),encoding="utf-8-sig"))
KEY=d["Jwt:Key"].encode(); ISS=d["Jwt:Issuer"]; AUD=d["Jwt:Audience"]
NI="http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
def b64(x): return base64.urlsafe_b64encode(x).rstrip(b"=").decode()
def jwt(uid):
    h=b64(json.dumps({"alg":"HS256","typ":"JWT"}).encode()); now=int(time.time())
    p=b64(json.dumps({NI:uid,"exp":now+3600,"iat":now,"nbf":now,"iss":ISS,"aud":AUD}).encode())
    return f"{h}.{p}."+b64(hmac.new(KEY,f"{h}.{p}".encode(),hashlib.sha256).digest())
u1="55aa7595-7896-4d73-871b-de17f8adb51c"; u2="e20e3edf-1240-4616-b15a-c87438be8940"; T={u1:jwt(u1),u2:jwt(u2)}
B="http://127.0.0.1:5077/api/games"
def call(user,method,path,body=None):
    req=urllib.request.Request(B+path,method=method,headers={"Authorization":"Bearer "+T[user],"Content-Type":"application/json"},data=json.dumps(body).encode() if body is not None else None)
    try: r=urllib.request.urlopen(req,timeout=30); raw=r.read(); return r.status,(json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        raw=e.read()
        try: return e.code,json.loads(raw)
        except: return e.code,raw[:120].decode(errors="replace")
    except urllib.error.URLError: return None,None
for i in range(60):
    if call(u1,"GET","/me")[0]: break
    time.sleep(1)
ok=lambda c,msg: print(("PASS " if c else "FAIL ")+msg)
s,m=call(u1,"POST","/matches",{"game":"chess","rated":True,"vsComputer":False,"seat":"p1"}); mid=m["id"]; ok(s==200 and m["status"]=="open","create chess challenge")
s,m=call(u2,"POST",f"/matches/{mid}/join"); ok(s==200 and m["status"]=="active" and len(m["board"]["legal"])==0,"join as p2 (no legal list off-turn)")
ok(call(u2,"POST",f"/matches/{mid}/move",{"move":"e7e5"})[0]==403,"off-turn move rejected 403")
ok(call(u1,"POST",f"/matches/{mid}/move",{"move":"e2e5"})[0]==400,"illegal move rejected 400")
for who,mv in [(u1,"e2e4"),(u2,"e7e5"),(u1,"d1h5"),(u2,"b8c6"),(u1,"f1c4"),(u2,"g8f6"),(u1,"h5f7")]:
    s,m=call(who,"POST",f"/matches/{mid}/move",{"move":mv})
ok(m["status"]=="finished" and m["winner"]=="p1" and m["endReason"]=="checkmate" and m["board"]["check"] and m["ratingDeltaP1"]>0,"scholar's mate: checkmate, p1 wins, Elo applied "+str((m["ratingDeltaP1"],m["ratingDeltaP2"])))
s,m=call(u1,"POST","/matches",{"game":"chess","rated":False,"vsComputer":False,"seat":"p1"}); mid2=m["id"]; call(u2,"POST",f"/matches/{mid2}/join")
s,m=call(u1,"POST",f"/matches/{mid2}/draw",{"action":"offer"}); ok(s==200 and m["drawOfferBy"]=="p1","draw offer")
s,m=call(u2,"POST",f"/matches/{mid2}/draw",{"action":"accept"}); ok(s==200 and m["isDraw"] and m["endReason"]=="drawAgreed","draw accepted")
s,m=call(u1,"POST","/matches",{"game":"connect4","rated":False,"vsComputer":False,"seat":"p1"}); mid3=m["id"]; call(u2,"POST",f"/matches/{mid3}/join")
ok(call(u1,"POST",f"/matches/{mid3}/draw",{"action":"offer"})[0]==400,"connect4 refuses draw offers")
s,m=call(u1,"POST",f"/matches/{mid3}/resign"); ok(s==200 and m["winner"]=="p2" and m["endReason"]=="resigned","resign")
s,m=call(u1,"POST","/matches",{"game":"connect4","rated":True,"vsComputer":False,"seat":"p1"}); mid4=m["id"]
ok(call(u2,"POST",f"/matches/{mid4}/cancel")[0]==403 and call(u1,"POST",f"/matches/{mid4}/cancel")[0]==204,"cancel permissions")
s,m=call(u1,"POST","/matches",{"game":"tictactoe","rated":True,"vsComputer":True,"difficulty":"hard","seat":"p1"}); ok(s==200 and m["status"]=="active" and not m["rated"] and m["p2"]["userId"] is None,"ttt vs computer: active, forced casual")
n=0
while m["status"]=="active" and n<9:
    free=[i for i,c in enumerate(m["board"]["cells"]) if c==0]; pick=4 if 4 in free else free[0]
    s,m=call(u1,"POST",f"/matches/{m['id']}/move",{"move":str(pick)}); n+=1
ok(m["status"]=="finished","ttt finished: winner=%s draw=%s"%(m["winner"],m["isDraw"]))
s,m=call(u1,"POST","/matches",{"game":"connect4","rated":False,"vsComputer":True,"difficulty":"easy","seat":"p1"}); n=0
while m["status"]=="active" and n<25:
    cells=m["board"]["cells"]; col=next(c for c in [3,2,4,1,5,0,6] if cells[0][c]==0)
    s,m=call(u1,"POST",f"/matches/{m['id']}/move",{"move":str(col)}); n+=1
ok(m["status"]=="finished" and (m["board"]["winningCells"] or m["isDraw"]),"connect4 vs computer finished in %d moves, winner=%s"%(n,m["winner"]))
s,m=call(u1,"POST","/matches",{"game":"chess","rated":False,"vsComputer":True,"difficulty":"easy","seat":"p2"}); ok(s==200 and m["moveCount"]==1 and m["turn"]=="p2","computer as white opens")
s,w=call(u1,"GET","/wordle/today"); ok(s==200 and w["mode"]=="daily" and w.get("answer") is None,"wordle today, answer hidden")
ok(call(u1,"POST","/wordle/guess",{"word":"ZZZZZ"})[0]==400,"wordle rejects non-word")
s,lb=call(u1,"GET","/leaderboard?game=chess"); ok(s==200 and len(lb)>=2 and lb[0]["rank"]==1,"leaderboard lists %d players"%len(lb))
s,me=call(u1,"GET","/me"); ok(s==200 and me["ratings"] and me["ratings"][0]["rank"] is not None,"me: rating card with rank "+str([(r["rating"],r["rank"]) for r in me["ratings"]]))
ok(call(u1,"GET","/history?limit=5")[0]==200,"history")
ok(call(u1,"GET","/players/"+u2+"?game=chess")[0]==200,"player page")
