if(isObject($Eventide_BillboardGroup))
{
    $Eventide_BillboardGroup.removeAllBillboards();
    $Eventide_BillboardGroup.delete();
}
$Eventide_BillboardGroup = new ScriptGroup(Eventide_BillboardGroup) { class="EventideClientBillboardGroup"; };

//
// Support functions.
//

//Adapted from ReviveOverlay.gui in Client_BlockOps.
function SimObject::clientEstimateHackPosition(%player)
{
    //`1.325` was obtained from setting the player's position to "0 0 0", then running `getHackPosition`.
	%hackOffset = 1.325 * getWord(%player.getScale(), 2);
	%playerPosition = VectorAdd(%player.getPosition(), "0 0" SPC %hackOffset);
	return %playerPosition;
}

//From ReviveOverlay.gui in Client_BlockOps.
function SimObject::clientEstimateFirstPersonEyeVector(%player)
{
	if(!(%player.getType() & $TypeMasks::PlayerObjectType))
    {
        return %player.getMuzzleVector(0);
    }
		
	for(%i = 0; %i < 4; %i++)
	{
		if(%player.getMountedImage(%i) == 0)
        {
            return %player.getMuzzleVector(%i);
        }
	}
	return %player.getMuzzleVector(3);
}

//From ReviveOverlay.gui in Client_BlockOps.
function SimObject::clientEstimateThirdPersonEyeVector(%player)
{
    %eyeVector = %player.clientEstimateFirstPersonEyeVector(); //Eye vector.
    %yaw = mAtan(getWord(%eyeVector, 1), getWord(%eyeVector, 0));
    %pitch = mAcos(getWord(%eyeVector, 2)) + %player.getDataBlock().cameraTilt;

    %x = 1 * mSin(%pitch) * mCos(%yaw);
	%y = 1 * mSin(%pitch) * mSin(%yaw);
	%z = 1 * mCos(%pitch);

    return VectorNormalize(%x SPC %y SPC %z);
}

//Adapted from ReviveOverlay.gui in Client_BlockOps.
function SimObject::clientEstimateThirdPersonEyePoint(%player)
{
	%playerDatablock = %player.getDatablock();
	%playerPosition = %player.clientEstimateHackPosition();
	%eyeVector = %player.clientEstimateThirdPersonEyeVector();

	%playerScaleY = getWord(%player.getScale(), 1);
	%cameraDistance = -(%playerDatablock.cameraMaxDist) * %playerScaleY;

	%offset = VectorScale(%eyeVector, %cameraDistance);
	%offset = setWord(%offset, 2, getWord(%offset, 2) + %playerDatablock.cameraVerticalOffset);

	return VectorAdd(%playerPosition, %offset);
}

//Adapted from ReviveOverlay.gui in Client_BlockOps.
function SimObject::clientEstimateFirstPersonEyePoint(%player)
{
	if(!isObject(%player))
    {
        return "0 0 0";
    }
	else if(!(%player.getType() & $TypeMasks::PlayerObjectType))
    {
        return %player.getPosition();
    }

	%playerPosition = %player.getPosition();
	%playerScale = %player.getScale();

    if(%player.isCrouched() || %player.isCrouched)
    {
        //Values obtained from setting the player's position to "0 0 0", then running `getEyePoint`.
        //0.1408854275941849 0.008123164065182209 0.6266684532165527
        %cameraOffsetX = 0.1408854275941849 * getWord(%playerScale, 0);
        %cameraOffsetY = 0.008123164065182209 * getWord(%playerScale, 1);
        %cameraOffsetZ = 0.6266684532165527 * getWord(%playerScale, 2);
    }
    else
    {
        //Values obtained from setting the player's position to "0 0 0" while crouching, then running `getEyePoint`.
        //0.1408854275941849 0.008123164065182209 2.156496524810791
        %cameraOffsetX = 0.1408854275941849 * getWord(%playerScale, 0);
        %cameraOffsetY = 0.008123164065182209 * getWord(%playerScale, 1);
        %cameraOffsetZ = 2.156496524810791 * getWord(%playerScale, 2);
    }

    %finalOffsetVector = %cameraOffsetX SPC %cameraOffsetY SPC %cameraOffsetZ;
	return VectorAdd(%playerPosition, %finalOffsetVector);
}

//From ReviveOverlay.gui in Client_BlockOps.
function SimObject::clientEstimateEyeTransform(%player)
{
    %cameraPosition = $firstPerson ? %player.clientEstimateFirstPersonEyePoint() : %player.clientEstimateThirdPersonEyePoint();
    %muzzleVector = $firstPerson ? %player.clientEstimateFirstPersonEyeVector() : %player.clientEstimateThirdPersonEyeVector();
	%forwardVector = vectorNormalize(setWord(%muzzleVector, 2, 0));

	%yaw = mAtan(getWord(%forwardVector, 0), getWord(%forwardVector, 1)) + 3.14159265;
	if(%yaw >= 3.14159265)
    {
        %yaw -= 6.2831853; 
    }

	return %cameraPosition SPC eulerToAxis(mAsin(vectorDot(vectorNormalize(%muzzleVector), "0 0 1")) * -57.2958 @ " 0 " @ %yaw * -57.2958);
}

//Adapted from ReviveOverlay.gui in Client_BlockOps.
function approximateWorldToScreenSpace(%cameraTransform, %targetPosition, %horizontalFOV, %screenWidth, %screenHeight)
{
    //https://forum.blockland.us/index.php?topic=209588.msg5869436#msg5869436
	%offset = MatrixMulVector("0 0 0" SPC getWords(%cameraTransform, 3, 5) SPC -1 * getWord(%cameraTransform, 6), VectorSub(%targetPosition, %cameraTransform));
	%x = getWord(%offset, 0);
	%y = getWord(%offset, 1);
	%z = getWord(%offset, 2);

    //If the point is behind the camera, return a blank string to signal this.
    if(%y >= 0)
    {
        return "";
    }

	%fovFactor = %y * mTan(%horizontalFOV * $pi / 360);

	%screenX = ((%x / %fovFactor) + 1) / 2 * %screenWidth;
	%screenY = %screenHeight - ((%z / %fovFactor * %screenWidth) - %screenHeight) / -2;

	return mFloor(%screenX + 0.5) SPC mFloor(%screenY + 0.5);
}

//
// Billboard object.
//

function EventideClientBillboard::onAdd(%this)
{
    //Verify the billboard object has an on-screen bitmap element to move around.
    if(!isObject(%this.bitmap))
    {
        error("No bitmap supplied during creation of billboard" SPC %this.getId() @ ", deleting...");
        %this.delete();
        return;
    }
    else if(!isFile(%this.bitmap.bitmap))
    {
        error("Invalid bitmap source file supplied during creation of billboard" SPC %this.getId() @ ", deleting...");
        %this.delete();
        return;
    }
    else
    {
        %bitmapClass = %this.bitmap.getClassName();
        if(%bitmapClass !$= "GuiBitmapCtrl" && %bitmapClass !$= "GuiAnimatedBitmapCtrl")
        {
            error("Invalid bitmap supplied during creation of billboard" SPC %this.getId() @ ", deleting...");
            %this.delete();
            return;
        }
    }

    //Verify that there is an object for the bitmap to hover over.
    if(%this.ghostId $= "")
    {
        error("No ghost object supplied during creation of billboard" SPC %this.getId() @ ", deleting...");
        %this.delete();
        return;
    }

    //Verify that there is an object for the bitmap to hover over.
    if(%this.serverObjectId $= "")
    {
        error("No server object supplied during creation of billboard" SPC %this.getId() @ ", deleting...");
        %this.delete();
        return;
    }

    //The object's position needs to be sent from the server manually. Let's start with a value.
    if(%this.objectPosition $= "")
    {
        error("No target position supplied during creation of billboard" SPC %this.getId() @ ", deleting...");
        %this.delete();
        return;
    }

    //I assume GUI updates are a little expensive, so use this variable to make sure we don't do them unless we have to.
    if(%this.isParked $= "")
    {
        %this.isParked = false;
    }

    %this.positionUpdateSchedule = 0;
    %this.positionUpdateRate = 500;
}

function EventideClientBillboard::updatePosition(%this)
{
    CommandToServer('EventideGetBillboardObjectPosition', %this.serverObjectId);
}

//
// Billboard group.
//

function EventideClientBillboardGroup::onAdd(%this)
{
    %this.tickRate = 50;
    %this.positionUpdateRate = 500;
    %this.billboardDictionary["initialized"] = true;
}

function EventideClientBillboardGroup::tick(%this)
{
    if(!isObject(%this) || %this.getCount() == 0)
    {
        warn("Eventide_BillboardGroup is empty, ceasing tick loop...");
        return;
    }

    //Common values that only need to be fetched once.
    %resolutionX = getResolutionX();
    %resolutionY = getResolutionY();
    %horizontalFOV = getHorizontalFOV();
    %verticalFOV = getVerticalFOV();
    %halfVerticalFOVRadians = mDegToRad(%verticalFOV / 2);
    //%halfHorizontalFOVRadians = mDegToRad(%horizontalFOV / 2);

    //For each billboard object in the rendering group...
    for(%i = 0; %i < %this.getCount(); %i++)
    {
        %billboardObject = %this.getObject(%i);

        //If the target object on the server still exists, the ghost ID will resolve. If not, we don't need the billboard anymore.
        %targetObject = ServerConnection.resolveGhostID(%billboardObject.ghostId);
        if(%targetObject == 0)
        {
            warn("Target object of billboard" SPC %billboardObject.getId() SPC "no longer exists, deleting...");
            $Eventide_BillboardGroup.removeBillboard(%billboardObject);
            continue;
        }

        //Determine where we are looking and where we are.
        %selfPlayer = ServerConnection.getControlObject();
        %selfEyeTransform = %selfPlayer.clientEstimateEyeTransform();

        //Get the size of the bitmap.
        %bitmapResolutionX = getWord(%billboardObject.bitmap.extent, 0);
        %bitmapResolutionY = getWord(%billboardObject.bitmap.extent, 1);
        %bitmapAspectRatio = (%bitmapResolutionX / %bitmapResolutionY);
        
        //Make the billboard scale in size based on distance.
        //More linear algebra. This is apparently standard projection stuff, according to ChatGPT.
        //TODO: No perspective scaling for FOV.
        %bitmapWorldSize = 4;
        %distanceToTarget = mClampF(VectorDist(%selfPlayer.getPosition(), %billboardObject.objectPosition), 0.001, 600);
        %bitmapPixelHeight = %bitmapWorldSize * (%resolutionY / (2 * %distanceToTarget * mTan(%halfVerticalFOVRadians)));
        %billboardObject.bitmap.extent = mCeil(%bitmapPixelHeight * %bitmapAspectRatio) SPC mCeil(%bitmapPixelHeight);

        //Using the target object's position along with the camera position and angle, calculate where the billboard should be
        //in screen space.
        %billboardVerticalOffset = getWord(%targetObject.getScale(), 2) * 5; //5 units upward per unit of scale on the Z-axis.
        %targetPlayerPosition = VectorAdd(%billboardObject.objectPosition, "0 0" SPC %billboardVerticalOffset); //The billboard should be above the target's head, not in it.
        %guiCoordinates = approximateWorldToScreenSpace(%selfEyeTransform, %targetPlayerPosition, %horizontalFOV, %resolutionX, %resolutionY);
        
        //The object is off-screen, "park" the bitmap.
        if(%guiCoordinates $= "")
        {
            if(!%billboardObject.isParked)
            {
                %billboardObject.bitmap.position = mCeil(%bitmapResolutionX * -1) SPC mCeil(%bitmapResolutionY * -1);
            }
            %billboardObject.isParked = true;
            continue;
        }
        else
        {
            %billboardObject.isParked = false;
        }

        //Adjust the screen coordinates to center the bitmap.
        %screenX = getWord(%guiCoordinates, 0) - (%bitmapResolutionX / 2);
        %screenY = getWord(%guiCoordinates, 1) - (%bitmapResolutionY / 2);
        %finalScreenCoordinates = mCeil(%screenX) SPC mCeil(%screenY);

        //Finally, move the bitmap object to the correct position.
        %billboardObject.bitmap.position = %finalScreenCoordinates;
    }

    cancel(%this.updateSchedule);
    %this.updateSchedule = %this.schedule(%this.tickRate, "tick");
}

function EventideClientBillboardGroup::removeBillboard(%this, %serverObjectId)
{
    %billboardObject = $Eventide_BillboardGroup.getBillboard(%serverObjectId);
    if(%billboardObject $= "")
    {
        return;
    }

    %billboardObject.bitmap.delete();
    %billboardObject.delete();

    if(%this.getCount() == 0 && isEventPending(%this.updateSchedule))
    {
        cancel(%this.updateSchedule);
        %this.updateSchedule = 0;
    }
}

function EventideClientBillboardGroup::removeAllBillboards(%this)
{
    while(%this.getCount() > 0)
    {
        %billboardObject = %this.getObject(0);
        %this.removeBillboard(%billboardObject);
    }
}

function EventideClientBillboardGroup::addBillboard(%this, %serverObjectId, %ghostId, %objectPosition, %bitmapPath)
{
    %bitmapSize = (getResolutionY() * 0.1);
    %bitmapObject = new GuiBitmapCtrl("EventideBillboard" @ %this.getCount())
    {
        profile = "GuiDefaultProfile";
        bitmap = %bitmapPath;
        horizSizing = "right";
        vertSizing = "bottom";
        extent = %bitmapSize SPC %bitmapSize;
        minExtent = %bitmapSize SPC %bitmapSize;
        position = -%bitmapSize SPC -%bitmapSize;
        enabled = "1";
        visible = "1";
    };
    PlayGui.add(%bitmapObject);

    %billboardObject = new ScriptObject()
    {
        class = "EventideClientBillboard";
        bitmap = %bitmapObject;
        defaultBitmapSize = %bitmapSize SPC %bitmapSize;
        ghostId = %ghostId;
        serverObjectId = %serverObjectId;
        objectPosition = %objectPosition;
    };
    %this.add(%billboardObject);
    %this.billboardDictionary[%serverObjectId] = %billboardObject;

    //Plan to update the billboard's position by contacting the server to request it.
    %billboardObject.positionUpdateSchedule = %billboardObject.schedule(%billboardObject.positionUpdateRate, "updatePosition");

    if(!isEventPending(%this.updateSchedule))
    {
        %this.tick();
    }
}

function EventideClientBillboardGroup::getBillboard(%this, %serverObjectId)
{
    return %this.billboardDictionary[%serverObjectId];
}

//
// Client-sided functions.
//

function clientCmdEventideAddBillboard(%serverObjectId, %ghostId, %objectPosition, %bitmapPath)
{
    $Eventide_BillboardGroup.addBillboard(%serverObjectId, %ghostId, %objectPosition, %bitmapPath);
}

function clientCmdEventideReceiveBillboardPosition(%serverObjectId, %objectPosition)
{
    %billboardObject = $Eventide_BillboardGroup.getBillboard(%serverObjectId);
    if(!isObject(%billboardObject))
    {
        return;
    }

    %billboardObject.objectPosition = %objectPosition;

    cancel(%billboardObject.positionUpdateSchedule);
    %billboardObject.positionUpdateSchedule = %billboardObject.schedule(%billboardObject.positionUpdateRate, "updatePosition");
}

function clientCmdEventideRemoveBillboard(%serverObjectId)
{
    $Eventide_BillboardGroup.removeBillboard(%serverObjectId);
}

function clientCmdEventideRemoveAllBillboards()
{
    $Eventide_BillboardGroup.removeAllBillboards();
}