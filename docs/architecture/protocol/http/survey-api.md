# /survey/api — Survey Endpoints

## Overview

The survey API was used by the live Darkspore service to deliver in-game survey prompts to players (e.g., post-session feedback). In the private server context, it is a near-stub that returns an empty survey list.

C++ router: `Game/API.cpp:577`. Dispatch chain: `API.cpp:583–587`.  
C# controller: `Adapters/Rest/SurveyRestController.cs` (`[RestController(Value="/survey/api")]`).

## Endpoint Table

| Method / Route | Query `method=` | C++ handler | C# handler | Status | Notes |
|---|---|---|---|---|---|
| GET/POST `/survey/api` | `api.survey.getSurveyList` | `API.cpp:584` → `survey_survey_getSurveyList():1958` | `SurveyRestController.cs:20` | ✅ | Returns empty `<surveys/>` list |
| GET/POST `/survey/api` | *(any other method)* | `API.cpp:586` → `empty_xml_response()` | *(throws UnimplementedMethodException)* | ⚠️ | C++ returns empty XML envelope; C# throws |

## api.survey.getSurveyList — Response Shape

Content-Type: `text/xml`

Query params: `version` (ignored in both implementations), `method`.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<response>
  <stat>ok</stat>
  <version>5.3.0.127</version>
  <timestamp>1</timestamp>
  <exectime>1</exectime>
  <surveys/>   <!-- always empty; no surveys are configured -->
</response>
```

If surveys were present, each would be:
```xml
<surveys>
  <survey>
    <id>some_id</id>
    <trigger1>0</trigger1>
    <trigger2>0</trigger2>
  </survey>
</surveys>
```

This shape is documented in a comment at `API.cpp:1963-1972`.

## Notes

Both C++ and C# produce identical responses. No survey data is seeded in either implementation. The `SurveyService.getSurveyList()` call in C# returns an empty collection from `SurveyRestController.cs:27`.
