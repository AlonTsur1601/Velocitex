namespace Velocitex.Core.Rooms;

public sealed record RoomCatalogEntry(
    int Number,
    string Id,
    string DisplayName,
    string ScenePath,
    string MechanicLabel,
    string PostRoomSpeaker,
    string PostRoomDialogue);

public static class RoomCatalog
{
    private static readonly RoomCatalogEntry[] AvailableRooms =
    {
        new(1, "room-01", "The Drop", "res://scenes/MovementTestRoom.tscn", "GROUND ROLL", "CHILD", "Is it still coming?"),
        new(2, "room-02", "The Copper Bend", "res://scenes/Room02.tscn", "SLOPE CONTROL", "MOTHER", "The machine is taking its time."),
        new(3, "room-03", "The Open Chute", "res://scenes/Room03.tscn", "AIRBORNE MOMENTUM", "CHILD", "I heard something fall in there."),
        new(4, "room-04", "The Relay Lock", "res://scenes/Room04.tscn", "INTERACTION / E", "MOTHER", "Maybe the mechanism needs another push."),
        new(5, "room-05", "The Proof Run", "res://scenes/Room05.tscn", "CHAPTER TEST", "CHILD", "I think it moved!"),
        new(6, "room-06", "Glass Drift", "res://scenes/Room06.tscn", "FRICTIONLESS", "MOTHER", "Now I can hear it rolling."),
        new(7, "room-07", "Caramel Brake", "res://scenes/Room07.tscn", "STICKY SURFACE", "CHILD", "Why does it sound so sticky?"),
        new(8, "room-08", "Blue Boost", "res://scenes/Room08.tscn", "ACCELERATOR", "MOTHER", "That sounded much faster."),
        new(9, "room-09", "Spring Vault", "res://scenes/Room09.tscn", "SUPER-ELASTIC", "CHILD", "Did it just bounce?"),
        new(10, "room-10", "Surface Circuit", "res://scenes/Room10.tscn", "SURFACE EXAM", "MOTHER", "That sounded like the whole machine."),
        new(11, "room-11", "Featherfall", "res://scenes/Room11.tscn", "LOW GRAVITY", "CHILD", "It sounds like it stopped falling."),
        new(12, "room-12", "Heavy Drop", "res://scenes/Room12.tscn", "STRONG GRAVITY", "MOTHER", "That sounded like a hard landing."),
        new(13, "room-13", "Crosswind", "res://scenes/Room13.tscn", "WIND", "CHILD", "I can hear air rushing inside."),
        new(14, "room-14", "Magnetic Rise", "res://scenes/Room14.tscn", "MOMENTUM RAIL", "MOTHER", "Something just clicked into place."),
        new(15, "room-15", "Gravity Circuit", "res://scenes/Room15.tscn", "GRAVITY EXAM", "CHILD", "That one sounded really long."),
        new(16, "room-16", "Bullseye", "res://scenes/Room16.tscn", "PLAYER CANNON", "MOTHER", "That sounded like it launched something."),
        new(17, "room-17", "Crossfire", "res://scenes/Room17.tscn", "INTERFERENCE CANNON", "CHILD", "Something is bouncing around in there!"),
        new(18, "room-18", "Rising Transit", "res://scenes/Room18.tscn", "MOVING PLATFORM", "MOTHER", "I think something just went up."),
        new(19, "room-19", "Piston Arc", "res://scenes/Room19.tscn", "MOMENTUM PISTON", "CHILD", "That was a really loud spring."),
        new(20, "room-20", "Ballistic Assembly", "res://scenes/Room20.tscn", "BALLISTIC EXAM", "MOTHER", "That sounded like the whole launcher assembly."),
        new(21, "room-21", "Soft Landing", "res://scenes/Room21.tscn", "MOMENTUM ABSORBER", "CHILD", "It suddenly went very quiet."),
        new(22, "room-22", "Ratchet Rise", "res://scenes/Room22.tscn", "ONE-WAY GRIP", "MOTHER", "It sounds like a tiny ratchet turning."),
        new(23, "room-23", "Flywheel Vault", "res://scenes/Room23.tscn", "MOMENTUM BANK", "CHILD", "Something in there just wound up!"),
        new(24, "room-24", "Break Point", "res://scenes/Room24.tscn", "BRITTLE BARRIER", "MOTHER", "I hope that cracking sound was normal."),
        new(25, "room-25", "Processing Line", "res://scenes/Room25.tscn", "PROCESSING EXAM", "CHILD", "Why does it keep changing how it rolls?"),
        new(26, "room-26", "Zero-G Crossfire", "res://scenes/Room26.tscn", "LOW-GRAVITY RINGS + CROSSFIRE", "CHILD", "Those cannons are tracking it through the air!"),
        new(27, "room-27", "Polarity Gauntlet", "res://scenes/Room27.tscn", "CROSSING MAGNETIC RAILS + LOW-G RING", "CHILD", "Did the candy just change direction?"),
        new(28, "room-28", "Counterweight", "res://scenes/Room28.tscn", "COUNTERWEIGHT", "CHILD", "It must be close now!"),
    };

    public static IReadOnlyList<RoomCatalogEntry> All => AvailableRooms;

    public static RoomCatalogEntry? Find(int roomNumber)
    {
        return AvailableRooms.FirstOrDefault(room => room.Number == roomNumber);
    }
}
