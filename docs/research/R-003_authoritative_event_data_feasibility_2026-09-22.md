# R-003 — Authoritative event data feasibility

**Date:** 2026-09-22
**Scope:** feasibility research only. This artifact does not select an MVP, redesign a feature, or authorize a production integration.
**Evidence labels:** **CONFIRMED** means an opened primary source directly supports the statement; **NOT FOUND** means the listed official sources were checked but did not document it; **UNKNOWN / BLOCKED** means answering would require a source, account, role, contract, or test not available to this team.

## 1. Research question

Can an independent MAX chatbot or mini-app lawfully and technically obtain, for a specific apartment building, an authoritative utility-interruption record containing its owner/source, publication and update time, planned end, operational ETA (if it exists), extensions, closure, and actual restoration status?

The question is about the entire chain, not merely whether data exists somewhere:

```text
record exists -> a named owner creates it -> an allowed interface exposes it
-> the team can lawfully obtain it -> a third-party MAX product can use it
```

## 2. Why this changes a product decision

`CURRENT.md` identifies `officially known event -> decide whether to act -> take the next official step after the expected deadline` as a candidate for evaluation, not an approved pivot. R-002 found no documented direct access for a third-party MAX product. R-003 remains decision-relevant because the event record itself, its lifecycle, and the access boundary determine whether that candidate can ever be a real-data scenario rather than a labelled demonstration.

If the chain is not demonstrated, the product must not tell a resident that an event is officially known, that they should wait, or that service has been restored.

## 3. Relevant hackathon requirements

Only the following requirements from the attached hackathon brief directly affect this research question:

| PDF page | Requirement relevant to R-003 | Consequence |
|---|---|---|
| 6 | The project must solve a relevant management or housing problem, define a concrete audience/problem, show one end-to-end scenario, and explain a measurable effect. | An event screen alone is not evidence of value; its data and next action must support a complete scenario. |
| 7 | MAX is the interaction environment; the main scenario must be testable in MAX. A mini-app is attached to a bot, not an isolated service. | A web page in GIS Housing is not automatically a MAX integration. |
| 7 | If an MVP uses test, prepared, or modelled data instead of a real external-system connection, this must be explicitly stated in materials. | A prepared event dataset is permitted only with prominent modelling labels. |
| 8 | Prefer a primary source; check data relevance/currentness; distinguish fact, product processing, recommendation, and assumption; do not imitate integrations. | Source, update/freshness, and uncertainty must be shown separately. |
| 17 | MVP is the minimal version in which the user obtains the main result; define the boundary of scope. | No claim of an end-to-end official-data outcome without the missing evidence. |
| 18 | Show which information is from an official source, calculated/processed by the product, a recommendation, or modelled data; do not present a model assumption as an official fact. | Any fallback must name its data class and limitation. |
| 19 | Scaling must separate an invariant core from region-specific data, rules, organisations, and integrations. | Event coverage and access rights are variable, not a national invariant. |

## 4. Method

Before browsing, the working evidence matrix required evidence for: record existence; event category; address scope; fields; owner; timestamps; lifecycle; public display; machine-readable read interface; write interface; roles; registration/certificates; partner route; MAX mechanism; and a separately documented operational ETA.

The research then followed the official documentation tree: public GIS Housing interruption page -> organisation registry -> record placement/change/history/export -> external-system documentation -> MAX developer documentation. Search results and previous artifacts were used only as leads. The claims below rely on the opened official sources listed in section 25.

## 5. Evidence matrix

| Required element | Status | Direct evidence | Limit |
|---|---|---|---|
| Plan interruption record exists | **CONFIRMED** | GIS Housing has a public page for planned interruptions and an organisation registry. | This does not establish an unplanned/emergency record. |
| Address-level selection | **CONFIRMED** | Public search accepts year and house address; placement selects buildings/premises/rooms. | Coverage depends on a record having been placed. |
| Utility/service and reason fields | **CONFIRMED** | Placement form contains interruption type, utility-service type, interruption type, reason, start/end, optional additional information. | Enumerated values were not exposed by the opened text. |
| Planned start/end | **CONFIRMED** | Placement requires date/time of start and end; public page is explicitly for planned interruptions. | This is planned schedule, not operational ETA. |
| Source/owner | **CONFIRMED at organisation-record level** | An organisation may change only records it placed; rights are assigned to organisation representatives. | A public record's displayed owner field was not confirmed. |
| Publication/update timestamp | **UNKNOWN** | Placement, new version, and event-history operations are documented. | Opened sources do not document a field exposed to the resident or a third-party API. |
| Extension/change lifecycle | **CONFIRMED for organisation records** | Changes produce a new version; successful placement replaces the previous version; history logs placement/edit/annulment. | Public consumer presentation of versions was not documented. |
| Closure/annulment | **CONFIRMED for organisation records** | Registry documents annulment and the history journal logs it. | No confirmed user-facing "service restored" status. |
| Actual restoration | **NOT FOUND** | Checked interruption placement/change/history/public pages. | None documents an actual-restoration field or event. |
| Machine-readable interface | **CONFIRMED, but role-bound** | GIS Housing documents web services for search/view/export as well as placement/change/annulment. | Current service specification and read-operation field list could not be retrieved from the Regulations archive. |
| Public API / anonymous export | **NOT FOUND** | Checked public interruption page and open-data documentation lead. | Public page documents a human search/subscription flow, not an API or anonymous file export. |
| Ordinary hackathon team access | **UNKNOWN / BLOCKED** | Organisation roles and external-IS exchange are documented. | Team has no documented GIS Housing organisation role, registered IS, certificate, or partner mandate. |
| MAX mechanism that supplies GIS Housing data | **NOT FOUND** | MAX Bridge/API document MAX client, bot, and user/platform functions. | They do not document a GIS Housing or Gosuslugi Dom event-data feed. |

## 6. Event/data model

The confirmed model is a **planned interruption record** in GIS Housing. Its placement form includes:

```text
interruption type
utility-service type
interruption kind
reason (+ free-text reason when "other")
start date/time
end date/time
additional information (optional)
target housing object: premises / building / room
```

The system applies checks that the start is not earlier than the current date, the end is not more than one year ahead, the period does not conflict with an existing placed planned-interruption record, and relevant service contracts exist for the target objects. Those checks support the conclusion that this evidence concerns planned notices, not an operational incident feed.

## 7. Data-field matrix

| Field / concept | Evidence status | What is actually evidenced | What must not be inferred |
|---|---|---|---|
| Known event | **CONFIRMED** for planned interruption | A placed planned-interruption record can be searched by house/year. | That every outage has such a record. |
| Authoritative owner | **CONFIRMED** at placement level | A record can be changed only by the organisation that placed it. | That the owner is exposed to public users or a bot. |
| Publication time | **UNKNOWN** | Placement and history operations exist. | A resident-visible or API-readable publication timestamp. |
| Update time | **UNKNOWN** | New versions and event history exist. | A usable freshness timestamp in the event payload. |
| Planned end | **CONFIRMED** | The form has end date/time. | Operational ETA or actual restoration. |
| Operational ETA | **NOT FOUND** | No opened source defines such a field for interruptions. | Renaming planned end as ETA. |
| Extension | **CONFIRMED** as an edit/version process | The placing organisation can edit its record; a successful new version replaces the old one. | That an extension is visible or pushed to a third-party app. |
| Closure / annulment | **CONFIRMED** as registry lifecycle | Annulment and history are documented. | That annulment proves service restoration. |
| Actual restoration | **NOT FOUND** | No field or event documented in sources examined. | That end time means water/power is back. |

## 8. Access model

Three materially different access paths are documented:

1. **Public human lookup.** The open GIS Housing page lets a person search planned interruptions by year and house address. It also offers subscription after authorisation. This is a user interface, not a documented public data API.
2. **Organisation registry.** UO/TSZh/RSO representatives receive specific rights to view or manage the interruption registry. These roles allow search, view, placement, editing, and annulment within the organisation context.
3. **External information system.** GIS Housing documentation states that an organisation or individual entrepreneur in the housing sector with its own information system can establish SOAP information exchange with GIS Housing. The regulations archive is the stated source for current formats and operations.

The second and third paths do not establish that a random MAX bot may call an endpoint. They require a qualifying organisation/IS context and its access administration.

## 9. Planned interruptions

**CONFIRMED.** The public page is explicitly titled "Information about planned interruptions in provision of utility services." It provides a search by year and house address. The organisation workflow records start/end and creates notifications for connected personal accounts when a planned interruption is placed or changed; automatic notification is described for ten working days before the planned start.

This gives a source-backed, human-readable route for a particular planned event if it was placed in the system. It does not show that every provider publishes all planned work or that an event is current at the moment a user opens a bot.

## 10. Unplanned / emergency interruptions

**UNKNOWN.** The opened primary documentation was specific to planned interruption records. It did not document an emergency/accident record, an emergency ETA, a provider's operational incident feed, or a public search for unplanned outages. The generic form has a "type of interruption" field, but its values and semantics were not shown in the opened source; this cannot establish an unplanned-event data model.

No planned-record field is used here as evidence for an emergency model.

## 11. Planned end vs operational ETA

**CONFIRMED:** planned interruption records contain planned start and end date/time.
**NOT FOUND:** a documented operational ETA, live recalculation, or a statement that planned end equals a restoration forecast.
**NOT FOUND:** a documented actual-restoration timestamp.

Therefore the artifact uses **planned end** only. A product must not call it "ETA", "time to restore", or "service restored" without another primary source.

## 12. Update / extension / closure / freshness

**CONFIRMED:** placement/edit/annulment operations exist. Editing a placed record may create a new version; after successful checks the new version replaces the previous one. The event-history journal records placement, editing, and annulment operations.

**UNKNOWN:** whether the event-history dates are delivered through the relevant read web service; whether an external product can identify the latest version; whether residents see version history; and whether a change is a service extension rather than a correction.

**NOT FOUND:** a documented freshness SLA, webhook, polling guarantee, or actual-restoration field for a third-party bot.

## 13. Machine-readable interfaces

**CONFIRMED:** official GIS Housing help says that search/view/export and placement/change/annulment of interruption data are available through web services; current descriptions are in the "Regulations and instructions" archive. The RSO export page also documents xlsx export of a selected interruption sample.

**CONFIRMED:** the external-information-system documentation identifies SOAP as the exchange protocol for eligible operators.

**UNKNOWN / BLOCKED:** the current web-service specification could not be retrieved in this research session: the official Regulations archive landing page timed out. Consequently this artifact does not claim a particular operation name, authentication flow, read scope, pagination, or field schema.

## 14. Who is allowed to use them

**CONFIRMED:** organisation representatives need assigned registry rights. Official privilege tables describe viewing and managing the interruption registry for authorised specialists in UO, TSZh, and RSO contexts.

**CONFIRMED:** an "operator of an information system" is described as an organisation or individual entrepreneur in the housing sector that processes significant information through its own IS; it can establish SOAP exchange with GIS Housing. Organisation administration includes an IS application and certificate handling.

**UNKNOWN:** whether this team meets the qualifying role; which exact rights a partner would delegate; and whether that partner's service scope includes the required house/event records.

## 15. MAX-specific findings

MAX Bridge is a client-side library that exposes MAX client/UI/user-context functions to a mini-app. MAX Bot API documents authenticated calls to the MAX platform API. The documented MAX materials checked do not describe a mechanism that automatically transfers GIS Housing or Gosuslugi Dom interruption data to an independent bot or mini-app.

**DOCUMENTED ACCESS NOT FOUND:** no opened `dev.max.ru` document exposed a GIS Housing event feed, a Gosuslugi Dom data-sharing API, or a bridge method that yields a utility-interruption record.

This means neither "MAX cannot do it" nor "the API does not exist". It means the documented mechanism was not found in the official MAX sources examined.

## 16. Disconfirming evidence checked

| Candidate alternative | Result |
|---|---|
| Public/anonymous route | A public human search page for planned interruptions exists. No public API or anonymous structured export was documented. |
| Organisation read/export | Confirmed: role-bound registry search/view and xlsx export exist; web services are documented as an integration channel. |
| External IS / SOAP | Confirmed as an official exchange model for eligible housing-sector organisations or entrepreneurs. Current event-read schema remains blocked by unavailable Regulations archive. |
| Partner route | Plausible only through a qualifying organisation that can lawfully authorise the data and access. No partner, mandate, or rights were evidenced for this team. |
| Test contour | The official documentation describes registration/certificate administration but the research did not obtain a test-contour account or current service specification. |
| MAX native integration | Not found in opened MAX Bridge, Bot API, or partner-service documentation. |

## 17. CONFIRMED

- GIS Housing has a planned-interruption record and public search by year and house address.
- The record workflow supports fields for service, cause, target housing object, planned start, planned end, and optional additional information.
- The placing organisation is the record-level owner for edits; placed records can be changed, versioned, annulled, exported, and have an event history.
- Role-bound registry access and web-service integration exist in GIS Housing.
- An eligible housing-sector operator IS can establish SOAP exchange with GIS Housing.
- MAX has bot and mini-app APIs, but those APIs describe MAX platform interaction.

## 18. CONTRADICTED

- **"A planned end is an operational ETA."** Contradicted by the absence of a source defining planned end as an operational restoration forecast.
- **"The existence of GIS Housing web services gives an independent MAX bot access."** Contradicted by the documented organisation roles, IS administration, and scope of MAX APIs.
- **"A cancellation/annulment proves the service was restored."** Contradicted: the documentation describes a registry operation, not a factual restoration field.

## 19. NOT FOUND

In the official sources checked on 2026-09-22, the following were not documented:

- public REST/JSON API or anonymous machine-readable export for planned interruptions;
- documented operational ETA, actual restoration time, or explicit service-restored status;
- documented public/third-party access to event publication or update timestamps;
- documented notification/webhook/polling SLA for an independent bot;
- documented MAX mechanism automatically supplying GIS Housing/Gosuslugi Dom event data;
- primary-source documentation of an unplanned/emergency interruption data model in the opened materials.

## 20. UNKNOWN / BLOCKED

- Exact current SOAP operation(s), request/response schema, and read-field coverage for interruption records: **BLOCKED** because the official Regulations archive could not be fetched during this session.
- Whether web services expose event history, latest-version metadata, source/owner, or timestamps: **UNKNOWN** until the current specification is obtained.
- Whether an eligible partner may lawfully supply the required record to this project and on what terms: **BLOCKED** by missing partner, mandate, organisation account, and rights.
- Whether a particular real house has complete planned-event coverage: **UNKNOWN** without a tested address and authorised walkthrough.
- Whether unplanned/emergency events are represented with equivalent data: **UNKNOWN**.

## 21. Feasibility implications

For the team as currently evidenced, an automatic answer such as "this is an officially known outage; wait until 20:00" is not supportable. The chain breaks at current authorised machine-readable read access and at freshness/actual-restoration semantics.

The public planned-interruption page supports a narrower, honest path: allow the user to open or perform an official lookup themselves. It does not license the bot to present the result as its own live integration.

## 22. Honest MVP fallback options

| Option | Technical position | Organisational/legal position | Required label / allowed claim |
|---|---|---|---|
| A. Direct integration | **UNKNOWN** until current SOAP read specification and access are verified. | **BLOCKED** without qualifying organisation/IS rights. | Do not claim now. |
| B. Partner/organisation integration | **Potentially feasible** because role-bound registry and SOAP exchange are documented. | Requires an eligible partner, rights, registration/certificate, field-level validation, and permission to use data in MAX. | "Data received from [partner]; scope and update time shown" only after evidence. |
| C. Official public data | **Feasible for user-assisted lookup** of planned interruptions by house/year. | Public page is documented; subscription requires authorisation. | "Open official GIS Housing search"; do not call it an API integration. |
| D. User-assisted deep link | **Feasible** using a user action to open the official page. | Does not transfer data into the bot. | "Check planned interruptions in GIS Housing". |
| E. Prepared source-backed dataset | **Feasible for demonstration**. | Only if provenance, date, territory, and limitations are recorded. | "Prepared demonstration dataset; not live GIS Housing data." |
| F. Synthetic/demo data | **Feasible for interaction testing**. | Permitted by the brief only with explicit marking. | "Synthetic scenario; no real integration or current outage claim." |

## 23. Kill conditions

Do not build a live-event recommendation as the core scenario if any of the following remains true at decision time:

1. No authorised source can provide a house-level record with provenance and a usable update/freshness indicator.
2. The current web-service contract does not expose the fields needed for the proposed claim.
3. The only date available is a planned end, but the product would need an operational ETA or actual restoration.
4. A qualifying partner cannot grant lawful, stable access for the target houses/services.
5. A task test shows that the official public lookup already resolves the resident's task without material friction.

## 24. What evidence could change the result

The following primary evidence would change the status:

1. The current official GIS Housing web-service specification showing read operations and fields for interruption record, latest update, owner/source, history, and status.
2. Written confirmation from an eligible UO/RSO/operator IS of the applicable role, access grant, data-use permission, coverage, and freshness semantics.
3. An authorised test-contour or production walkthrough for one real house that records request, response, timestamps, updates, and access controls.
4. Official documentation of an unplanned/emergency record and an operational ETA or actual-restoration field.
5. Official MAX documentation for a GIS Housing/Gosuslugi Dom data interface, if one is published.

## 25. Primary sources visited

All URLs were accessed on 2026-09-22.

1. **Hackathon brief "Smart City"** — MAX hackathon — attached PDF, pp. 6–8, 17–19, 21. Relevant requirements: MAX scenario, data provenance/freshness, modelling disclosure, MVP boundary, scaling, and official-source starting points.
2. **Information about planned interruptions in provision of utility services** — GIS Housing / AO "Operator of Information System" — https://cdn.dom.gosuslugi.ru/webhelp/topics/public_part/pauses_public_part-och.html . Relevant section: public search by year and house address; subscription flow.
3. **Placement of information about an interruption in provision of utility services** — GIS Housing — https://cdn.dom.gosuslugi.ru/webhelp/topics/objects/supply_pause_list/add-tko.html . Relevant section: record fields, housing-object scope, checks, placed status, planned notifications.
4. **Changing interruption information** — GIS Housing — https://cdn.dom.gosuslugi.ru/webhelp/topics/objects/supply_pause_list/edit-uo.html . Relevant section: only the placing organisation can edit; new versions replace previous records after checks.
5. **Event history view** — GIS Housing — https://cdn.dom.gosuslugi.ru/webhelp/rso/topics/objects/supply_pause_list/history.html . Relevant section: placement/edit/annulment operations in the event journal.
6. **Export of interruption information from the system** — GIS Housing — https://cdn.dom.gosuslugi.ru/webhelp/rso/topics/objects/supply_pause_list/export.html . Relevant section: selected-sample xlsx export; search/view/export web services; link to current regulations archive.
7. **Documentation on GIS Housing** — GIS Housing — https://cdn.dom.gosuslugi.ru/webhelp/new/topics/introduction/c_documentation-aoss.html . Relevant section: publicly available Regulations-and-Instructions archive for external-IS formats.
8. **Operator of an information system** — GIS Housing — https://cdn.dom.gosuslugi.ru/webhelp/new/topics/omnibus/c_operator_is-operator_is.html . Relevant section: eligible housing-sector organisation/entrepreneur and SOAP exchange; IS application and certificate administration.
9. **Access rights of managing-organisation staff** — GIS Housing — https://cdn.dom.gosuslugi.ru/webhelp/new/reusables/r_privileges-uo.html . Relevant section: view/manage interruption registry as authorised specialist.
10. **MAX Bridge** — MAX for Developers — https://dev.max.ru/docs/webapps/bridge . Relevant section: WebApp client/UI/user-context functionality.
11. **MAX API overview** — MAX for Developers — https://dev.max.ru/docs-api . Relevant section: authenticated MAX platform API and documented scope.
12. **Service selection for MAX Partner Platform** — MAX for Developers — https://dev.max.ru/docs/maxbusiness/selectionservices . Relevant section: verified legal entity/individual entrepreneur/self-employed eligibility, bot moderation, mini-app attachment.

## 26. Search limits and reason for stopping

The research checked the official public planned-interruption page, organisation workflows for placement/change/history/export, role tables, operator-IS guidance, the official external-IS documentation landing page, and MAX Bridge/Bot/partner documentation. It also ran targeted searches for unplanned/emergency records, public API/open data, web-service search/read operations, and MAX-to-GIS integration.

Further browsing stopped because the unresolved cells now require either (a) the current Regulations archive/service specification, whose official landing page timed out, or (b) an authorised organisation/partner account and a real-house walkthrough. Repeating broad searches would not resolve those access-controlled facts.

---

## Final status

# UNKNOWN

**Evidence that determines this status:** planned interruption records, public human lookup, organisation lifecycle, and role-bound SOAP integration are documented; the current evidence does not establish the read schema, team authorisation, event freshness fields, operational ETA, actual restoration, or a MAX data channel.

**Evidence that would change this status:** the current official read-service specification plus an eligible partner/organisation grant and a successful authorised end-to-end walkthrough for a real house.
