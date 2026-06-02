# EAWebKit modern engine — Phase 2 (JS bridge) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Make the MiniBlink-rendered web UI interactive by wiring its JavaScript ↔ EAWebKit's native callback bridge, so the launcher's buttons (and later the in-game web pages) work — i.e. JS calls like `EA.callSporeNet(...)` reach the game via `ViewNotification::JavascriptMethodInvoked`, and return values flow back.

**Architecture:** `mpWebView` is NULL under MiniBlink, so EAWebKit's WebCore-bound `JavascriptBindingObject` cannot be used. Instead, un-stub `CreateJavascriptBindings`/`RegisterJavascriptMethod` to build a `window.<bindingName>.<method>` object in MiniBlink (via `mbJsBindFunction` + a tiny `mbRunJs` glue), whose native thunk fills a `JavascriptMethodInvokedInfo` and calls the host's `ViewNotification::JavascriptMethodInvoked`. recapmb owns all mb/JS-value details and exposes a neutral `JsVal` boundary; the View does `JsVal`↔`JavascriptValue`.

**Tech Stack:** C++ (VC9/EAWebKit), MiniBlink `mb` API (`mb132_x32.dll`/`mb.h`). Builds on Phase 1 (`recapmb`, `RECAP_MINIBLINK`).

**Spec:** `docs/superpowers/specs/2026-06-02-eawebkit-modern-engine-design.md` (Phase 2).
**Prereq:** Phase 1 done — the launcher RENDERS in-game (proven 2026-06-02). Memory: [[eawebkit-modern-engine-phase1]].

**Scope note (from the user):** Phase 1 proved the **launcher** (a standalone OS window, `ClientUI::CreateLauncherWindow`@0x00513d10, input forwarded by `ClientUI::LauncherWndProc`@0x00513870). The **in-game** web pages (login/store inside the game) use the **composited cWebView → D3D9 texture** path (`ClientWeb::WebView_UpdateTexture`@0x00ca4e90), which is a DIFFERENT display path we have not reached yet (you only get there after the launcher's Play button works). This same JS bridge serves both; the in-game *display* path is validated separately once Play works. So Phase 2's gate = the launcher becomes clickable and Play advances.

**Real APIs (gathered):**
- mb (all `__stdcall`): `void mbJsBindFunction(const char* name, mbJsNativeFunction fn, void* param, unsigned argCount)`; `jsValue` accessors `mbGetJsValueType(es,v)`, `mbJsToString(es,v)→const utf8*`, plus `mbJsToDouble`/`mbJsToInt`/`mbJsToBoolean` and builders `mbDoubleToJsValue`/`mbStringToJsValue`/`mbInt32ToJsValue`/`mbJsUndefined` (confirm exact names in `mb.h` — the `js`/`mbJs` value family near `mbGetJsValueType`); `mbRunJs(view, frame, script, isInClosure, cb, param, 0)` and `mbRunJsSync(view, frame, script, isInClosure)→mbJsValue`; `mbWebFrameGetMainFrame(view)`. `mbJsNativeFunction` ≈ `jsValue (__stdcall*)(jsExecState es, void* param)`; arg count/getter `jsArgCount(es)` / `jsArg(es,i)` (mb exposes the wke-compat `js*` names; verify in `mb.h`).
- EAWebKit: `JavascriptMethodInvokedInfo { const char* mMethodName; unsigned mArgumentCount; JavascriptValue mArguments[MAX_ARGUMENTS]; JavascriptValue mReturn; }`; `ViewNotification::JavascriptMethodInvoked(JavascriptMethodInvokedInfo&)`; `JavascriptValue`: `GetType()`, `SetNumberValue(double)`, `SetBooleanValue(bool)`, `SetStringType()`+`GetStringValue().SetCharacters(const char16_t*, len)`, `SetUndefined()`, `GetNumberValue()`, `GetStringValue()` (EASTLFixedString16Wrapper → `GetCharacters()`), types `JavascriptValueType_{Undefined,Number,String,Boolean,Array,Object}`.

---

## File Structure

| File | Responsibility |
|---|---|
| `eawebkit/source/recapmb.h` (EDIT) | Add the neutral JS boundary: `JsVal` POD, `MbJsSink` callback, `MbCreateJsObject`, `MbBindJsMethod`, `MbRunJs`. |
| `eawebkit/source/recapmb.cpp` (EDIT) | Implement the above: `mbJsBindFunction` per method + `mbRunJs` glue to hang it on `window.<obj>.<method>`; the mb native thunk marshals mb `jsValue`↔`JsVal` and calls the stored `MbJsSink`. |
| `eawebkit/source/EAWebKitView.cpp` (EDIT) | Un-stub `CreateJavascriptBindings`/`RegisterJavascriptMethod`/`EvaluateJavaScript` under `RECAP_MINIBLINK`; provide the View `MbJsSink` that builds `JavascriptMethodInvokedInfo`, calls `JavascriptMethodInvoked`, and marshals `JsVal`↔`JavascriptValue`. Trim Phase-1 debug logging. |

No new files. The bridge reuses Phase-1 plumbing.

---

## Task 1: recapmb — neutral JS boundary types + API (header)

**Files:** Modify `C:\CodingProjects\Personal\eawebkit\source\recapmb.h`

- [ ] **Step 1: Add the JS types/API to `recapmb.h`** (inside `namespace recap`, after the load-sink block)

```cpp
/* Neutral JS value crossing the recapmb<->View boundary (no mb or EAWebKit types leak). */
enum JsValType { kJsUndefined = 0, kJsNumber, kJsString, kJsBoolean };
struct JsVal {
    JsValType   type;
    double      num;     /* number; also 0/1 for boolean */
    const char* str;     /* UTF-8; valid only during the sink call */
};

/* The View implements this. Called (UI thread) when a bound JS method fires.
   args[0..argc-1] are inputs; fill *ret for the JS return (default undefined). */
typedef void (*MbJsSink)(void* user, const char* method, const JsVal* args, int argc, JsVal* ret);

/* Create window.<objectName> = {} (idempotent) for the view. */
void MbCreateJsObject(MbView*, const char* objectName);
/* Bind window.<objectName>.<method> -> native; the view's sink(user,...) handles it. */
void MbBindJsMethod(MbView*, const char* objectName, const char* method, MbJsSink sink, void* user);
/* Evaluate script in the main frame (fire-and-forget). */
void MbRunJs(MbView*, const char* script);
```

- [ ] **Step 2: Commit** — none (eawebkit tree is not git).

---

## Task 2: recapmb — implement the JS bridge (mb side)

**Files:** Modify `C:\CodingProjects\Personal\eawebkit\source\recapmb.cpp`

- [ ] **Step 1: Store the sink on the view + a bound-method registry**

In `struct MbView`, add:
```cpp
    MbJsSink    jsSink;
    void*       jsUser;
```
(zero-inited by the existing `memset(self,0,sizeof(*self))` in `MbCreate`.)

The bound method name must reach the thunk. mb's `mbJsBindFunction` takes a `void* param`; pass a small heap record per method:
```cpp
struct RecapBoundMethod { MbView* view; char name[64]; };
```

- [ ] **Step 2: The native thunk (marshals mb jsValue <-> JsVal, calls the View sink)**

Add (near the other thunks). NB: confirm the exact mb arg accessors in `mb.h` (`jsArgCount`/`jsArg`/`mbGetJsValueType`/`mbJsToString`/`mbJsToDouble`/`mbJsToBoolean`) and the return builders; the shape is:
```cpp
static jsValue MB_CALL_TYPE recapJsThunk(jsExecState es, void* param)
{
    RecapBoundMethod* m = (RecapBoundMethod*)param;
    if (!m || !m->view || !m->view->jsSink) return jsUndefined();
    int argc = jsArgCount(es);
    if (argc > 8) argc = 8;
    JsVal args[8];
    for (int i = 0; i < argc; ++i)
    {
        jsValue v = jsArg(es, i);
        mbJsType t = mbGetJsValueType(es, v);
        if (t == kMbJsTypeNumber)      { args[i].type = kJsNumber;  args[i].num = mbJsToDouble(es, v); args[i].str = 0; }
        else if (t == kMbJsTypeBoolean){ args[i].type = kJsBoolean; args[i].num = mbJsToBoolean(es, v) ? 1 : 0; args[i].str = 0; }
        else if (t == kMbJsTypeString) { args[i].type = kJsString;  args[i].str = mbJsToString(es, v); args[i].num = 0; }
        else                           { args[i].type = kJsUndefined; args[i].num = 0; args[i].str = 0; }
    }
    JsVal ret; ret.type = kJsUndefined; ret.num = 0; ret.str = 0;
    mbLogf("JS call: %s argc=%d", m->name, argc);
    m->view->jsSink(m->view->jsUser, m->name, args, argc, &ret);
    if (ret.type == kJsNumber)  return mbDoubleToJsValue(es, ret.num);
    if (ret.type == kJsBoolean) return mbBoolToJsValue(es, ret.num != 0);
    if (ret.type == kJsString)  return mbStringToJsValue(es, ret.str ? ret.str : "");
    return jsUndefined();
}
```
(If a builder/getter name differs in this mb release, fix to the `mb.h` name — the semantics above are what to implement.)

- [ ] **Step 3: `MbCreateJsObject` / `MbBindJsMethod` / `MbRunJs`**

```cpp
void MbRunJs(MbView* v, const char* script)
{
    if (!v || !script) return;
    mbRunJs(v->wv, mbWebFrameGetMainFrame(v->wv), script, FALSE, 0, 0, 0);
}

void MbCreateJsObject(MbView* v, const char* obj)
{
    if (!v || !obj) return;
    char js[160];
    _snprintf(js, sizeof(js)-1, "window.%s = window.%s || {};", obj, obj); js[159] = 0;
    MbRunJs(v, js);
    mbLogf("MbCreateJsObject: window.%s", obj);
}

void MbBindJsMethod(MbView* v, const char* obj, const char* method, MbJsSink sink, void* user)
{
    if (!v || !obj || !method) return;
    v->jsSink = sink; v->jsUser = user;
    /* bind a unique global native fn, then alias window.<obj>.<method> to it */
    RecapBoundMethod* m = new RecapBoundMethod();
    m->view = v;
    { size_t i; for (i = 0; method[i] && i < sizeof(m->name)-1; ++i) m->name[i] = method[i]; m->name[i] = 0; }
    char global[96];
    _snprintf(global, sizeof(global)-1, "__recap_%s_%s", obj, method); global[95] = 0;
    mbJsBindFunction(global, recapJsThunk, m, 8);
    char js[256];
    _snprintf(js, sizeof(js)-1,
        "window.%s.%s=function(){return window.%s.apply(null,arguments);};", obj, method, global);
    js[255] = 0;
    MbRunJs(v, js);
    mbLogf("MbBindJsMethod: window.%s.%s -> %s", obj, method, global);
}
```
Notes: `mbJsBindFunction` registers a GLOBAL (e.g. `window.__recap_EA_callSporeNet`); the glue aliases `window.EA.callSporeNet` to it (forwarding all args). `_snprintf` is fine — recapmb.cpp defines `_CRT_SECURE_NO_WARNINGS`.

- [ ] **Step 4: Build the standalone smoke test for the bridge** (extend `tests/recapmb_smoke.cpp`)

After load, create an object + bind a method, run JS that calls it, assert the sink fired and the return reached JS:
```cpp
static int g_called = 0;
static void jsSink(void* /*user*/, const char* method, const recap::JsVal* args, int argc, recap::JsVal* ret)
{
    g_called = 1;
    printf("sink: %s argc=%d arg0=%s\n", method, argc, (argc && args[0].type==recap::kJsString) ? args[0].str : "(n/a)");
    ret->type = recap::kJsNumber; ret->num = 42;
}
/* ... after MbLoadHTML of a page, or load "about:blank" then: */
recap::MbCreateJsObject(v, "EA");
recap::MbBindJsMethod(v, "EA", "ping", jsSink, 0);
recap::MbRunJs(v, "window.__recap_test = EA.ping('hello');");
/* pump a bit, then read back: */ 
/* (optional) verify via mbRunJsSync("window.__recap_test") == 42 */
```
**Gate:** `sink: ping argc=1 arg0=hello` prints and `g_called==1`. Build/run like Phase 1 (`cl /MT /EHsc ... user32.lib gdi32.lib`, mb132_x32.dll next to the exe).

- [ ] **Step 5: Commit** — none (eawebkit tree).

---

## Task 3: EAWebKitView — wire CreateJavascriptBindings / RegisterJavascriptMethod / EvaluateJavaScript

**Files:** Modify `C:\CodingProjects\Personal\eawebkit\source\EAWebKitView.cpp`

- [ ] **Step 1: The View JS sink (JsVal -> JavascriptValue -> JavascriptMethodInvoked -> back)**

Add in the anonymous namespace (near `RecapMbLoadThunk`):
```cpp
void RecapMbJsSink(void* user, const char* method, const recap::JsVal* args, int argc, recap::JsVal* ret)
{
    EA::WebKit::View* v = (EA::WebKit::View*)user;
    EA::WebKit::ViewNotification* pVN = EA::WebKit::GetViewNotification();
    if (!v || !pVN) return;
    EA::WebKit::JavascriptMethodInvokedInfo info;
    info.mMethodName = method;
    info.mArgumentCount = (unsigned)argc;
    for (int i = 0; i < argc; ++i)
    {
        if (args[i].type == recap::kJsNumber)       info.mArguments[i].SetNumberValue(args[i].num);
        else if (args[i].type == recap::kJsBoolean) info.mArguments[i].SetBooleanValue(args[i].num != 0);
        else if (args[i].type == recap::kJsString)
        {
            info.mArguments[i].SetStringType();
            /* UTF-8 -> char16_t (ASCII-widen; launcher args are ASCII/JSON) */
            const char* s = args[i].str ? args[i].str : "";
            char16_t w[1024]; int n = 0; for (; s[n] && n < 1023; ++n) w[n] = (char16_t)(unsigned char)s[n]; w[n] = 0;
            info.mArguments[i].GetStringValue().SetCharacters(w, n);
        }
        else info.mArguments[i].SetUndefined();
    }
    info.mReturn.SetUndefined();
    pVN->JavascriptMethodInvoked(info);
    /* marshal the return back */
    switch (info.mReturn.GetType())
    {
    case EA::WebKit::JavascriptValueType_Number:  ret->type = recap::kJsNumber;  ret->num = info.mReturn.GetNumberValue(); break;
    case EA::WebKit::JavascriptValueType_Boolean: ret->type = recap::kJsBoolean; ret->num = info.mReturn.GetBooleanValue() ? 1 : 0; break;
    case EA::WebKit::JavascriptValueType_String:
    {
        static char buf[4096];
        const char16_t* w = info.mReturn.GetStringValue().GetCharacters();
        int n = 0; for (; w && w[n] && n < 4095; ++n) buf[n] = (char)w[n]; buf[n] = 0;
        ret->type = recap::kJsString; ret->str = buf; break;
    }
    default: ret->type = recap::kJsUndefined; break;
    }
}
```
(`GetBooleanValue()` — confirm the getter name in `EAWebKitJavascriptValue.h`; it mirrors `SetBooleanValue`. The string widen/narrow is ASCII-only for v1; the launcher's args/returns are ASCII/JSON.)

- [ ] **Step 2: `CreateJavascriptBindings` — create the JS object under the flag**

Replace the Phase-1 early-return stub:
```cpp
#ifdef RECAP_MINIBLINK
	if (mpRecapMb) { recap::MbLog("View::CreateJavascriptBindings (mb: skip WebCore JS-window bind; Phase 2 wires mb JS)"); return; }
#endif
```
with:
```cpp
#ifdef RECAP_MINIBLINK
	if (mpRecapMb)
	{
		recap::MbCreateJsObject((recap::MbView*)mpRecapMb, GetFixedString(mJavascriptBindingObjectName)->c_str());
		return;
	}
#endif
```

- [ ] **Step 3: `RegisterJavascriptMethod` — bind each method to mb**

At the top of `View::RegisterJavascriptMethod` (before the `if (mJavascriptBindingObject)`):
```cpp
#ifdef RECAP_MINIBLINK
	if (mpRecapMb)
	{
		recap::MbBindJsMethod((recap::MbView*)mpRecapMb,
			GetFixedString(mJavascriptBindingObjectName)->c_str(), name, RecapMbJsSink, this);
		return;
	}
#endif
```

- [ ] **Step 4: `EvaluateJavaScript` — route to mb**

At the top of `View::EvaluateJavaScript(const char16_t* pScriptSource, size_t length, ...)` body, add (narrow to UTF-8 ASCII):
```cpp
#ifdef RECAP_MINIBLINK
	if (mpRecapMb)
	{
		char buf[4096]; size_t n = 0; for (; n < length && n < 4095; ++n) buf[n] = (char)pScriptSource[n]; buf[n] = 0;
		recap::MbRunJs((recap::MbView*)mpRecapMb, buf);
		if (pReturnValue) pReturnValue->SetUndefined();
		return true;
	}
#endif
```
(The `const char*` overload at ~917 already forwards to this one, so both are covered.)

- [ ] **Step 5: Trim Phase-1 debug logging** (reduce noise now that it works)

- In `View::Tick` mb branch: remove the `s_tickN` logging (keep just `MbWake` + return). 
- In `recapmb.cpp` `paintHDC` / `PaintSink`: drop the per-paint `paintHDC #n` log and the BMP dump call + the sample-pixel log (keep `MbCreate`/`mbInit`/`document ready`/JS-call logs). Leave the crash-safe logger + the JS-call log.

- [ ] **Step 6: Commit** — none (eawebkit tree).

---

## Task 4: Build + in-game gate (USER-RUN)

- [ ] **Step 1: Build** `EAWebKit.sln` `pc-vc-dev-opt|Win32`. Expect `0 failed`. (recapmb.cpp verified standalone under `/W4 /WX` in each task; EAWebKitView.cpp edits are simple.)
- [ ] **Step 2: Deploy** the new `EAWebkit.dll` + `mb132_x32.dll` next to `Darkspore.exe` (keep `recap.cfg`).
- [ ] **Step 3: In-game gate** — launch.
  - **PASS:** the launcher is now **interactive** — hovering/clicking buttons responds, and the recap.cfg-served launcher JS round-trips (recapmb.log shows `JS call: <name>`). Clicking **Play** advances (the game starts loading). This unblocks reaching the in-game web pages.
  - Watch: which JS methods fire (recapmb.log `JS call:`) vs which the launcher needs — if a needed callback returns wrong/undefined and the launcher stalls, that method's `JavascriptMethodInvoked` handler (game-side) or its return marshalling needs attention.
  - **Next (Phase 3):** once Play works and the game starts, validate the **in-game composited web** (cWebView → D3D9 via `WebView_UpdateTexture`@0x00ca4e90). That display path differs from the launcher window; if those pages are black, apply the same notification/format learnings there (likely the per-frame `GetSurface()->GetData()` pull "just works", but confirm).
- [ ] **Step 4: Record milestone** in `memory/eawebkit-modern-engine-phase1.md` (or a new phase2 memory), commit ReCap-repo docs.

---

## Self-review notes
- Spec coverage: JS bridge (RegisterJavascriptMethod→mbJsBindFunction; JS→JavascriptMethodInvoked) = T2/T3; CreateJavascriptBindings object = T3.2; EvaluateJavaScript→mbRunJs = T3.4; standalone JS gate = T2.4; in-game gate = T4. Matches the modern-engine spec's Phase 2.
- Placeholders: code is concrete; two API-name confirmations are flagged inline (mb jsValue builder/accessor exact spellings in `mb.h`; `GetBooleanValue` in EAWebKitJavascriptValue.h) — verify-at-implementation, not TODOs. Arrays/objects as JS args/returns are deferred (launcher uses string/number/bool/JSON-as-string); note if a callback needs them.
- Type consistency: `recap::JsVal{type,num,str}`, `MbJsSink(user,method,args,argc,ret)`, `MbCreateJsObject/MbBindJsMethod/MbRunJs` identical across T1/T2/T3. `JavascriptMethodInvokedInfo`/`JavascriptValue` setters match EAWebKitJavascriptValue.h.
- Boundary: recapmb owns mb/jsValue; View owns JavascriptValue/JavascriptMethodInvoked — neither leaks the other's types (recapmb.h stays mb-free).
