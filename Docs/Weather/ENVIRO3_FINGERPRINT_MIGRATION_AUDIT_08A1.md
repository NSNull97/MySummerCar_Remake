# Enviro 3 fingerprint migration audit — 08A.1 world hardening

Date: 2026-07-20. Status: `AUDITED / PRODUCTION-UNUSED SAMPLE / MIGRATION APPROVED FOR VALIDATION`.

## Why the strict preflight stopped

The accepted 07B vendor snapshot no longer matched after Unity 6000.3.11f1
reserialized one Enviro URP sample material. The vendor file was not edited by
project code and is not used by the HDRP production runtime. The strict failure
was retained until the byte-level difference, reference graph and aggregate
fingerprint were audited.

## Aggregate fingerprints

| Snapshot | Files | Bytes | SHA-256 |
|---|---:|---:|---|
| Accepted canonical 07B | 538 | 305,967,931 | `8e376fa2748162157975fbdd8b1045e021b810fea40f01d99eafe22a4d30bd44` |
| Audited canonical 08A.1 | 538 | 305,968,075 | `a22883eaea25d7dca37c50429c59cff7e8cb6a61cd0686463801e10123d9f040` |

The file count is unchanged and the aggregate size increased by exactly 144
bytes.

## The only changed file

`Assets/Enviro 3 - Sky and Weather/Sample/Materials/Terrain Material URP.mat`

| Form | Bytes | SHA-256 |
|---|---:|---|
| Accepted 07B backup | 2,985 | `a3d675cfacdc163e358835080a06270c6d36f374a932d6dff493c1badd83ed9a` |
| Current Unity 6 form | 3,129 | `5df86e70d613c4454f60badf640ac4a999b7bbecb9dfdad9f9ba310c5c5d7fc2` |

Its `.meta` file is byte-identical in both snapshots: 188 bytes, SHA-256
`66705e657b74c13a3f3a59c4fc561b03b12cb612f220b6d9e0b8c128a751f960`,
GUID `fb81fa413942b134abb29cdcc93c8b72`.

The YAML diff contains only Unity serialization schema fields:

- MonoBehaviour importer version `4 -> 10`;
- Material serialized version `6 -> 8`;
- legacy empty `m_ShaderKeywords` replaced by the current empty
  parent/keyword/locking fields;
- empty `m_Ints` and `m_AllowLocking: 1` added.

The shader GUID, all texture references, floats and colours are unchanged.

## Reference and build audit

The material GUID is referenced only by the vendor scene
`Assets/Enviro 3 - Sky and Weather/Sample/Scene/Sample_URP.unity` (nine terrain
references). There are no references outside the Enviro vendor root. The URP
sample scene is absent from Build Settings and the project production runtime is
HDRP.

## Reproduction check

The exact `Enviro3PreflightValidator` algorithm was reproduced read-only with
the pinned `ru-RU` path comparer. Substituting only the accepted 2,985-byte form
into the current 538-file tree reproduces the 07B aggregate exactly:

`538 / 305967931 / 8e376fa2748162157975fbdd8b1045e021b810fea40f01d99eafe22a4d30bd44`.

Therefore no second changed vendor file is hidden by the aggregate migration.

## Decision and stability gate

Do not restore or patch the vendor asset. The preflight constants migrate
explicitly to the audited Unity 6 serialized snapshot. The migration is accepted
only if all of the following remain true after candidate generation and tests:

1. fresh preflight reports
   `538 / 305968075 / a22883eaea25d7dca37c50429c59cff7e8cb6a61cd0686463801e10123d9f040`;
2. production environment builder and validator pass;
3. focused Enviro and production weather tests pass;
4. the post-test vendor snapshot is byte-identical to the pre-test snapshot.

The historical 07B audit remains unchanged as provenance for the earlier
accepted snapshot.
