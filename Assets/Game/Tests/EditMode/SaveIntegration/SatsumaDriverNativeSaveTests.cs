using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Bootstrap;
using MSC.Player;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed partial class CanonicalConsumableNativeSaveTests
    {
        [Test]
        public void NativeDriverStateRoundTripsIntoFreshPresentationAndOldWalkingStateReleasesIt()
        {
            WithReadOnlyNativeEngine(document =>
            {
                SaveDocument saved;
                using (var source = EngineFixture())
                {
                    RestoreEngineDocument(source,document);
                    using var player = new DriverPlayerFixture(source);
                    Assert.That(player.Session.Station.DriverSeat.IsInstalled,Is.True);
                    Assert.That(player.Session.TryRestoreDrivingState(player.SeatedState(),out string failure),Is.True,failure);
                    var registry = DriverRegistry(source,player.Participant);
                    saved = new SaveDocumentCodec().Deserialize(new SaveDocumentCodec().Serialize(NewDocument(registry.CaptureDomains())),true);
                    Assert.That(player.Session.ValidateOwnerSavePayload(EngineEnvelope(saved).PayloadJson,out failure),Is.True,failure);
                }
                using var target = EngineFixture();
                using var restored = new DriverPlayerFixture(target);
                var targetRegistry = DriverRegistry(target,restored.Participant);
                var report = new UnresolvedContentReport();
                var prepared = targetRegistry.PrepareRestore(saved,report,target.Deferred);
                Assert.That(restored.Session.IsDriving,Is.False,"Preflight does not seat or create purchased parts.");
                targetRegistry.ApplyRestore(prepared,report,target.Deferred);
                Assert.That(restored.Session.IsDriving,Is.True);
                Assert.That(restored.Session.LocalYawDegrees,Is.EqualTo(27));
                Assert.That(restored.Motor.enabled || restored.Capsule.enabled,Is.False);
                Assert.That(target.Assembly.GetComponent<VehicleInputRouter>().ConsumeFixedInput(0).Throttle01,Is.Zero);
                AssertNativePurchases(target,document,EngineVehicle(NewDocument(target.Registry.CaptureDomains())));
                var walk = PlayerSaveDto.Create(new Vector3(10,5,20),Quaternion.identity,
                    FirstPersonMotorSaveDto.Create(PlayerPosture.Crouch,0),FirstPersonLookSaveDto.Create(0));
                saved.Domains.Single(e=>e.DomainId==PlayerSaveParticipant.DomainId).PayloadJson=SaveParticipantJson.Serialize(walk);
                prepared=targetRegistry.PrepareRestore(saved,report,target.Deferred);
                targetRegistry.ApplyRestore(prepared,report,target.Deferred);
                Assert.That(restored.Session.IsDriving,Is.False);
                Assert.That(restored.Motor.enabled && restored.Capsule.enabled,Is.True);
                Assert.That(restored.Player.transform.position,Is.EqualTo(walk.WorldPosition));
            });
        }

        [Test]
        public void DriverPreflightRejectsMissingOwnerSeatAndUnknownStationWithoutMutatingPlayer()
        {
            WithReadOnlyNativeEngine(document =>
            {
                using var f=EngineFixture(); RestoreEngineDocument(f,document);
                using var player=new DriverPlayerFixture(f);
                Assert.That(player.Session.TryRestoreDrivingState(player.SeatedState(),out _),Is.True);
                string before=player.Participant.CapturePayload();
                var envelope=new SaveDomainEnvelope { DomainId=PlayerSaveParticipant.DomainId,SchemaVersion=1,PayloadJson=before };
                var report=new UnresolvedContentReport();
                Assert.Throws<InvalidDataException>(()=>player.Participant.PrepareRestore(envelope,new SaveRestorePreparationContext(report,f.Deferred)));
                var badOwner=SaveParticipantJson.Deserialize<VehicleDomainSaveDto>(EngineEnvelope(document).PayloadJson);
                badOwner.vehicles[0].assembly.parts.Single(p=>p.partDefinitionId=="vehicle.satsuma.part.seat-driver").installedMountId=string.Empty;
                var owners=new Dictionary<string,SaveDomainEnvelope> { [VehicleSaveParticipant.DomainId]=new SaveDomainEnvelope
                    {DomainId=VehicleSaveParticipant.DomainId,SchemaVersion=1,PayloadJson=SaveParticipantJson.Serialize(badOwner)} };
                Assert.Throws<InvalidDataException>(()=>player.Participant.PrepareRestore(envelope,new SaveRestorePreparationContext(report,f.Deferred,owners)));
                var unknown=PlayerSaveDto.Create(Vector3.one*50,Quaternion.identity,FirstPersonMotorSaveDto.Create(PlayerPosture.Crouch,0),
                    FirstPersonLookSaveDto.Create(0),PlayerDrivingSaveDto.Create("vehicle.driver.unknown",0,Vector3.zero,PlayerPosture.Crouch));
                envelope.PayloadJson=SaveParticipantJson.Serialize(unknown);
                Assert.Throws<InvalidDataException>(()=>player.Participant.PrepareRestore(envelope,new SaveRestorePreparationContext(report,f.Deferred,owners)));
                Assert.That(player.Participant.CapturePayload(),Is.EqualTo(before));
            });
        }

        [Test]
        public void DriverTransactionFailureAfterPlayerApplyRestoresOriginalSeatedSession()
        {
            WithReadOnlyNativeEngine(document =>
            {
                using var f=EngineFixture(); RestoreEngineDocument(f,document);
                using var player=new DriverPlayerFixture(f);
                Assert.That(player.Session.TryRestoreDrivingState(player.SeatedState(),out _),Is.True);
                string before=player.Participant.CapturePayload();
                var fail=new DriverLateFailureParticipant();
                var registry=DriverRegistry(f,player.Participant,fail);
                SaveDocument incoming=NewDocument(registry.CaptureDomains());
                incoming.Domains.Single(e=>e.DomainId==PlayerSaveParticipant.DomainId).PayloadJson=SaveParticipantJson.Serialize(
                    PlayerSaveDto.Create(Vector3.one*10,Quaternion.identity,FirstPersonMotorSaveDto.Create(PlayerPosture.Crouch,0),FirstPersonLookSaveDto.Create(0)));
                var report=new UnresolvedContentReport();
                var prepared=registry.PrepareRestore(incoming,report,f.Deferred);
                Exception actualFailure=Assert.Catch(()=>registry.ApplyRestore(prepared,report,f.Deferred));
                Assert.That(fail.Applied,Is.True,"Failure must occur after the player really applied walking state: "+actualFailure);
                Assert.That(player.Session.IsDriving,Is.True);
                Assert.That(player.Participant.CapturePayload(),Is.EqualTo(before));
                Assert.That(player.Motor.enabled || player.Capsule.enabled,Is.False);
            });
        }

        private static SaveParticipantRegistry DriverRegistry(Fixture f,params ISaveParticipant[] additional)
        {
            return new SaveParticipantRegistry(f.Registry.OrderedParticipants.Concat(additional),f.RestorePlan);
        }
        private sealed class DriverLateFailureParticipant : ISaveParticipant
        {
            public bool Applied;
            public SaveParticipantDescriptor Descriptor {get;}=new("test.driver.late-failure",1,true,SaveRestorePhase.CarryState,PlayerSaveParticipant.DomainId);
            public string CapturePayload()=>"{}";
            public object PrepareRestore(SaveDomainEnvelope envelope,SaveRestorePreparationContext context)=>null;
            public object CaptureCheckpoint()=>null;
            public void ApplyPreparedRestore(object state,SaveRestoreContext context) {Applied=true;throw new InvalidOperationException("Injected driver rollback test.");}
            public void Rollback(object checkpoint) { }
        }
        private sealed class DriverPlayerFixture : IDisposable
        {
            public GameObject Player {get;}
            public FirstPersonMotor Motor {get;}
            public FirstPersonLook Look {get;}
            public CharacterController Capsule {get;}
            public Camera Camera {get;}
            public SatsumaDrivingSessionController Session {get;}
            public PlayerSaveParticipant Participant {get;}
            public DriverPlayerFixture(Fixture f)
            {
                // Instantiate under an inactive parent: no input-map mutation or
                // gameplay startup is needed to test native transactions.
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Player/Content/Prefabs/M4_FirstPersonPlayer.prefab");
                Player=Object.Instantiate(prefab,f.Assembly.transform.parent,false);
                Player.SetActive(false);
                Motor=Player.GetComponent<FirstPersonMotor>(); Look=Player.GetComponent<FirstPersonLook>();
                Capsule=Player.GetComponent<CharacterController>(); Camera=Player.GetComponentInChildren<Camera>(true);
                Session=Player.AddComponent<SatsumaDrivingSessionController>();
                Session.Initialize(Player.GetComponent<PlayerInputRouter>(),Motor,Look,Player.GetComponent<PlayerInteractionController>(),
                    Camera,f.Assembly.GetComponent<SatsumaDriverStation>(),f.Assembly.GetComponent<VehicleInputRouter>());
                Participant=new PlayerSaveParticipant(Player);
            }
            public PlayerDrivingSaveDto SeatedState()=>PlayerDrivingSaveDto.Create(Session.Station.StationId,27,
                new Vector3(-.282f,-.19307387f,-.06712156f),PlayerPosture.Crouch);
            public void Dispose() {if(Player!=null) Object.DestroyImmediate(Player);}
        }
    }
}
