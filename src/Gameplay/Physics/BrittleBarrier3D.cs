using Godot;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Gameplay.Physics;

public partial class BrittleBarrier3D : Node3D
{
    [Signal] public delegate void BrokenEventHandler(PlayerBall player, float impactSpeed);
    public Vector2 BarrierSize { get; set; } = new(9.0f, 5.5f);
    public float RequiredSpeed { get; set; } = 14.0f;
    public bool EnableAudio { get; set; } = true;
    public bool IsBroken { get; private set; }
    public float LastImpactSpeed { get; private set; }
    private StaticBody3D _body=null!; private CollisionShape3D _collision=null!; private MeshInstance3D _intactVisual=null!; private MeshInstance3D[] _shards=Array.Empty<MeshInstance3D>(); private Transform3D[] _startTransforms=Array.Empty<Transform3D>(); private AudioStreamPlayer3D? _audio; private Tween? _tween;
    public override void _Ready()
    {
        _body=new StaticBody3D{Name="BarrierBody",PhysicsMaterialOverride=new PhysicsMaterial{Friction=.5f,Bounce=.08f}};AddChild(_body);_collision=new CollisionShape3D{Shape=new BoxShape3D{Size=new Vector3(BarrierSize.X,BarrierSize.Y,.42f)}};_body.AddChild(_collision);
        StandardMaterial3D intactMaterial=RoomGeometry.CreateMaterial("res://assets/textures/brittle_sugar_glass_intact.svg",new Color("b8d6d0"),.08f,.48f);
        StandardMaterial3D shardMaterial=RoomGeometry.CreateMaterial("res://assets/textures/brittle_sugar_glass.svg",new Color("b8d6d0"),.08f,.48f);
        _intactVisual=new MeshInstance3D{Name="IntactPane",Mesh=Velocitex.Gameplay.Visual.SurfaceMeshFactory.CreateTiledBox(new Vector3(BarrierSize.X,BarrierSize.Y,.36f),4.0f),MaterialOverride=intactMaterial};_body.AddChild(_intactVisual);
        _shards=new MeshInstance3D[9];_startTransforms=new Transform3D[9];
        for(int row=0;row<3;row++)for(int col=0;col<3;col++){int i=row*3+col;MeshInstance3D shard=new(){Name=$"Shard{i}",Position=new Vector3((col-1)*(BarrierSize.X/3),((1-row)*(BarrierSize.Y/3)),0),Mesh=Velocitex.Gameplay.Visual.SurfaceMeshFactory.CreateTiledBox(new Vector3(BarrierSize.X/3-.06f,BarrierSize.Y/3-.06f,.36f),4.0f),MaterialOverride=shardMaterial,Visible=false};_body.AddChild(shard);_shards[i]=shard;_startTransforms[i]=shard.Transform;}
        Area3D sensor=new(){Name="ImpactSensor",Position=new Vector3(0,0,.48f),CollisionLayer=0,CollisionMask=1,Monitoring=true};sensor.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new Vector3(BarrierSize.X,BarrierSize.Y,1.35f)}});sensor.BodyEntered+=OnImpact;AddChild(sensor);
        if(EnableAudio){_audio=new AudioStreamPlayer3D{Name="BreakSfx",Stream=GD.Load<AudioStream>("res://assets/audio/sfx/device_sugar_glass_break.wav"),Bus="SFX",MaxDistance=45,UnitSize=7};AddChild(_audio);}
    }
    public void ResetBarrier(){_tween?.Kill();_tween=null;IsBroken=false;LastImpactSpeed=0;_collision.SetDeferred(CollisionShape3D.PropertyName.Disabled,false);_intactVisual.Visible=true;for(int i=0;i<_shards.Length;i++){_shards[i].Transform=_startTransforms[i];_shards[i].Transparency=0.0f;_shards[i].Visible=false;}_audio?.Stop();}
    private void OnImpact(Node3D body){if(IsBroken||body is not PlayerBall player)return;float speed=Mathf.Abs(player.LinearVelocity.Dot(-GlobalBasis.Z.Normalized()));LastImpactSpeed=speed;if(speed<RequiredSpeed)return;IsBroken=true;_collision.SetDeferred(CollisionShape3D.PropertyName.Disabled,true);_intactVisual.Visible=false;foreach(MeshInstance3D shard in _shards){shard.Visible=true;}_audio?.Play();_tween=CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);for(int i=0;i<_shards.Length;i++){Vector3 p=_shards[i].Position;Vector3 target=p+new Vector3(p.X*.22f,p.Y*.16f-2.6f,-1.6f-(i%3)*.25f);_tween.TweenProperty(_shards[i],"position",target,.68f);_tween.TweenProperty(_shards[i],"rotation",new Vector3((i-4)*.22f,(i%3-1)*.36f,(i%2==0?1:-1)*.42f),.68f);_tween.TweenProperty(_shards[i],"transparency",1.0f,.28f).SetDelay(.48f);}_tween.TweenCallback(Callable.From(()=>{foreach(MeshInstance3D shard in _shards){shard.Visible=false;}})).SetDelay(.8f);EmitSignal(SignalName.Broken,player,speed);}
}
