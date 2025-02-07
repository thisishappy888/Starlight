using Starlight;
using Swed64;
using System.Numerics;
using System.Runtime.InteropServices;

Swed swed = new Swed("cs2");

IntPtr client = swed.GetModuleBase("client.dll"); // get client.dll

Renderer renderer = new Renderer();
renderer.Start().Wait();
Vector2 screenSize = renderer.screenSize;

// offsets.cs
int dwLocalPlayerPawn = 0x1889F30;
int dwEntityList = 0x1A359C0;
int dwViewAngles = 0x1AABA50;
int dwViewMatrix = 0x1AA17C0;

// client.dll
int m_pCameraServices = 0x11E0;
int m_iFOV = 0x210;
int m_bIsScoped = 0x23E8;
int m_flFlashBangTime = 0x13F8;
int m_hPlayerPawn = 0x80C;
int m_iszPlayerName = 0x660;
int m_entitySpottedState = 0x23D0;
int m_bSpotted = 0x8;
int m_iHealth = 0x344;
int m_ArmorValue = 0x241C;
int m_vOldOrigin = 0x1324;
int m_iTeamNum = 0x3E3;
int m_vecViewOffset = 0xCB0;
int m_lifeState = 0x348;
int m_modelState = 0x170;
int m_pGameSceneNode = 0x328;



const int HOTKEY = 0x51;


List<Entity> entities = new List<Entity>();
Entity localPlayer = new Entity();

while (true)
{
    entities.Clear();

    uint desiredFov = (uint)renderer.fov;
    IntPtr localPlayerPawn = swed.ReadPointer(client, dwLocalPlayerPawn);
    IntPtr cameraServices = swed.ReadPointer(localPlayerPawn, m_pCameraServices);
    IntPtr entityList = swed.ReadPointer(client, dwEntityList);
    IntPtr listEntry = swed.ReadPointer(entityList, 0x10);

    localPlayer.pawnAddress = swed.ReadPointer(client, dwLocalPlayerPawn);
    localPlayer.team = swed.ReadInt(localPlayer.pawnAddress, m_iTeamNum);
    localPlayer.origin = swed.ReadVec(localPlayer.pawnAddress, m_vOldOrigin);
    localPlayer.view = swed.ReadVec(localPlayer.pawnAddress, m_vecViewOffset);

    for(int i = 0; i < 64; i++)
    {
        if(listEntry == IntPtr.Zero) 
            continue;
        IntPtr currentController = swed.ReadPointer(listEntry, i * 0x78);

        if (currentController == IntPtr.Zero) continue;

        int pawnHandle = swed.ReadInt(currentController, m_hPlayerPawn);
        if (pawnHandle == 0) continue;

        IntPtr listEntry2 = swed.ReadPointer(entityList, 0x8 * ((pawnHandle & 0x7FFF) >> 9) + 0x10);

        IntPtr currentPawn = swed.ReadPointer(listEntry2, 0x78 * (pawnHandle & 0x1FF));

        if (currentPawn == localPlayer.pawnAddress) continue;

        IntPtr sceneNode = swed.ReadPointer(currentPawn, m_pGameSceneNode);

        IntPtr boneMatrix = swed.ReadPointer(sceneNode, m_modelState + 0x80);

        int health = swed.ReadInt(currentPawn, m_iHealth);
        int team = swed.ReadInt(currentPawn, m_iTeamNum);
        uint lifeState = swed.ReadUInt(currentPawn, m_lifeState);


        if (lifeState != 256) continue;
        if (team == localPlayer.team && !renderer.aimOnTeam) continue;

        float[] viewMatrixESP = swed.ReadMatrix(client + dwViewMatrix);

        Entity entity = new Entity();

        //ESP
        entity.team = swed.ReadInt(currentPawn, m_iTeamNum);
        entity.position = swed.ReadVec(currentPawn, m_vOldOrigin);
        entity.viewOffset = swed.ReadVec(currentPawn, m_vecViewOffset);
        entity.position2D = Calculate.WorldToScreenESP(viewMatrixESP, entity.position, screenSize);
        entity.viewPosition2D = Calculate.WorldToScreenESP(viewMatrixESP, Vector3.Add(entity.position, entity.viewOffset), screenSize);

        entity.name = swed.ReadString(currentController, m_iszPlayerName, 16).Split("\0")[0];
        entity.pawnAddress = currentPawn;
        entity.controllAddress = currentController;
        entity.health = health;
        entity.lifeState = lifeState;
        entity.origin = swed.ReadVec(currentPawn, m_vOldOrigin);
        entity.view = swed.ReadVec(currentPawn, m_vecViewOffset);
        entity.distance = Vector3.Distance(entity.origin, localPlayer.origin);
        entity.head = swed.ReadVec(boneMatrix, 6 * 32); // 6 = bone id, 32 step between bones coordinates.

        ViewMatrix viewMatrix = ReadMatrix(client + dwViewMatrix);
        entity.head2d = Calculate.WorldToScreen(viewMatrix, entity.head, (int)screenSize.X, (int)screenSize.Y);
        entity.pixelDisctance = Vector2.Distance(entity.head2d, new Vector2(screenSize.X / 2, screenSize.Y / 2));
        entities.Add(entity);   

        if (renderer.enableRadarhack)
        {
            string name = swed.ReadString(currentController, m_iszPlayerName, 16);
            bool spotted = swed.ReadBool(currentPawn, m_entitySpottedState + m_bSpotted);

            swed.WriteBool(currentPawn, m_entitySpottedState + m_bSpotted, true);

            string spottedStatus = spotted == true ? "spotted" : " ";

            Console.WriteLine($"{name}: {spottedStatus}");
        }
    }
    renderer.UpdateLocalPlayer(localPlayer);
    renderer.UpdateEntities(entities);

    uint currentFov = swed.ReadUInt(cameraServices + m_iFOV);

    bool isScoped = swed.ReadBool(localPlayerPawn, m_bIsScoped);

    float flashDuration = swed.ReadFloat(localPlayerPawn, m_flFlashBangTime);

    if (renderer.enableAntiFlash)
    {
        if (flashDuration > 0)
        {
            swed.WriteFloat(localPlayerPawn, m_flFlashBangTime, 0);
            Console.WriteLine("Evaded flash!");
        }
    }

    if (renderer.enableFOV)
    {
        if (!isScoped && currentFov != desiredFov)
        {
            swed.WriteUInt(cameraServices + m_iFOV, desiredFov);
        }
    }
    //entities = entities.OrderBy(o => o.distance).ToList();
    entities = entities.OrderBy(o => o.pixelDisctance).ToList();

    if (entities.Count > 0 && GetAsyncKeyState(HOTKEY) < 0 && renderer.aimbot)
    {
        Vector3 playerView = Vector3.Add(localPlayer.origin, localPlayer.view);
        Vector3 entityView = Vector3.Add(entities[0].origin, entities[0].view);

        if (entities[0].pixelDisctance < renderer.aimbotFov)
        {
            Vector2 newAngles = Calculate.CalculateAngles(playerView, entities[0].head);
            Vector3 newAnglesVec3 = new Vector3(newAngles.Y, newAngles.X, 0.0f);

            swed.WriteVec(client, dwViewAngles, newAnglesVec3);
        }

        //Vector2 newAngles = Calculate.CalculateAngles(playerView, entities[0].head);
        //Vector3 newAnglesVec3 = new Vector3(newAngles.Y, newAngles.X, 0.0f);

        //swed.WriteVec(client, dwViewAngles, newAnglesVec3);

    }
}

[DllImport("user32.dll")]
static extern short GetAsyncKeyState(int vKey);

ViewMatrix ReadMatrix(IntPtr matrixAddress)
{
    var viewMatrix = new ViewMatrix();
    var matrix = swed.ReadMatrix(matrixAddress);

    viewMatrix.m11 = matrix[0];
    viewMatrix.m12 = matrix[1];
    viewMatrix.m13 = matrix[2];
    viewMatrix.m14 = matrix[3];

    viewMatrix.m21 = matrix[4];
    viewMatrix.m22 = matrix[5];
    viewMatrix.m23 = matrix[6];
    viewMatrix.m24 = matrix[7];

    viewMatrix.m31 = matrix[8];
    viewMatrix.m32 = matrix[9];
    viewMatrix.m33 = matrix[10];
    viewMatrix.m34 = matrix[11];

    viewMatrix.m41 = matrix[12];
    viewMatrix.m42 = matrix[13];
    viewMatrix.m43 = matrix[14];
    viewMatrix.m44 = matrix[15];

    return viewMatrix;
}