# Enviro 3: evidence версии

Дата среза: 2026-07-17.

## Вывод

Точную patch-версию локального payload нельзя доказать однозначно. Корректная маркировка до появления непротиворечивого vendor package metadata:

`Enviro 3.x local payload; code lineage includes changes through v3.0.8; exact installed patch UNKNOWN`

Нельзя сокращать это до уверенного `3.0.8` или `3.0.7a`.

## Прямые evidence

| Источник | Наблюдение | Оценка |
|---|---|---|
| `E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Enviro 3 - Sky and Weather\version.txt:1` | `Enviro 3.0.0` | похоже на не обновлённый заголовок |
| тот же `version.txt` | changelog содержит `v3.0.0`...`v3.0.8` | доказывает присутствие release notes до 3.0.8, но не package identity |
| `Scripts\Editor\Base\EnviroManagerInspector.cs:49` | inspector рисует `Version: 3.0.7` | противоречит концу changelog |
| `Scripts\Runtime\Modules\Fog\EnviroFogModule.cs` | присутствуют HDRP fog tint/control fields, соответствующие записи changelog 3.0.8 | косвенное evidence code lineage, не authoritative version metadata |
| Additional Pack `HowToUse.txt` | требует `Enviro 3.0.7a+` | требование add-on, не версия base package |

В корне нет `package.json`, Asset Store package manifest или другого локального единственного поля version, которому можно отдать приоритет.

## Hashes version evidence

- `version.txt`: `F4AEE13D2017B552DA61B5109B46BBBAA2A2592A99F4C03C9BF854AF3C92FCC7`;
- `EnviroManagerInspector.cs`: `447EE6D92B5CD8D7A08ACC94A5EF1E477CEAC38C44EB76602B7C21ACEC064304`;
- `EnviroManager.cs`: `15A35BD1306599D57F1F9CE69FA007F9A4C47766F280D3545519F93088D51D7C`;
- runtime asmdef: `5794678280E876518E20F038024992EF36CEAE834C0A0DDF45766736C893CC82`;
- editor asmdef: `701251F3A2184CA1CA01F06486C49C124CEA29F7AFF0FF814994C515AA10F686`.

Эти hashes позволяют точно распознать текущий локальный payload при следующем аудите, даже если маркетинговая версия остаётся неоднозначной.

## Compatibility evidence

Прямой metadata о поддержке Unity 6.3/HDRP 17.3 в payload не обнаружено. `version.txt` упоминает fixes/support для Unity 2022, Unity 2023.1+, HDRP 12+ и HDRP 15+, но это не гарантия Unity 6.

Фактическое локальное evidence сильнее:

- Unity `6000.3.11f1` импортировал `Enviro3.Runtime` и `Enviro3.Editor`;
- HDRP/URP/Core RP `17.3.0` удовлетворяют asmdef/shader dependencies;
- batchmode compile `M07A_EnviroHDRP_Compile.log` завершился code `0`;
- vendor obsolete warnings остались;
- headless WeatherLab runtime lifecycle/state mapping подтверждён; пиксельный output custom post process, fog/water visual compatibility и performance ещё не подтверждены.

Итог compatibility confidence:

| Область | Confidence |
|---|---|
| C# compilation с Unity 6.3.11f1 | High, фактически выполнено |
| Shader import после URP 17.3 | Medium/High, в последнем compile log нет прежних include errors |
| HDRP clouds/fog runtime structure | Medium/High: registration, single owners и headless lifecycle PASS; visual output pending |
| Additional Weather Pack | Unknown: base patch не доказан, pack не импортирован |

## Условие обновления документа

Указать exact version можно только при появлении одного из следующих локальных evidence:

1. vendor/Asset Store package metadata с однозначным version;
2. оригинальный `.unitypackage` manifest/version record;
3. официальный vendor marker, чей hash относится к этому payload;
4. подтверждение поставщика, однозначно сопоставленное текущим file hashes.
