namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>
/// Where the Seat's camera is in flight: a ground position, the height above the ground, the yaw (0 looks along +Y, a positive
/// turn looks left) and the pitch (the angle below the horizon, so a positive pitch looks down).
/// </summary>
internal readonly record struct SeatFlight(float X, float Y, float Height, float Yaw, float Pitch);

/// <summary>
/// One frame of flight input, already read from the engine. <c>Right</c> and <c>Forward</c> run -1 to 1, <c>Lift</c> is +1 to
/// rise and -1 to sink, <c>ZoomNotches</c> is positive for the wheel rolled away (it lowers the view), <c>LookYaw</c> is in look
/// units with a positive value turning left, and <c>LookPitch</c> is in look units with a positive value looking down.
/// </summary>
internal readonly record struct SeatInput(float Right, float Forward, float Lift, float ZoomNotches, float LookYaw, float LookPitch, bool Fast);

/// <summary>The camera in world terms: the position and the angles it looks with.</summary>
internal readonly record struct SeatPose(float X, float Y, float Z, float Yaw, float Pitch);
