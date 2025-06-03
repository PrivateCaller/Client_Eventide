$resolutionHash = $pref::Video::resolution;
$resolution = getWords($pref::Video::resolution, 0, 1);
$resolutionX = getWord($resolution, 0);
$resolutionY = getWord($resolution, 1);
$aspectRatio = ($resolutionX / $resolutionY);
$horizontalFOVHash = $Pref::Player::defaultFov;
$horizontalFOV = $Pref::Player::defaultFov;

function getAspectRatio()
{
    return $aspectRatio;
}

//Thanks ChatGPT.
function calculateVerticalFOV(%horizontalFOV, %aspectRatio)
{
    %horizontalFOV = $Pref::Player::defaultFov;
    %aspectRatio = getAspectRatio();

    %horizontalFOVRadians = mDegToRad(%horizontalFOV);
    %tanHalfHorizontalFOV = mTan(%horizontalFOVRadians / 2);
    %verticalFOVRadians = 2 * mATan(%tanHalfHorizontalFOV, %aspectRatio); //Atan2
    return mRadToDeg(%verticalFOVRadians);
}

$verticalFOV = calculateVerticalFOV($horizontalFOV, $aspectRatio);

function getResolution()
{
    return $resolution;
}

function getResolutionX()
{
    return $resolutionX;
}

function getResolutionY()
{
    return $resolutionY;
}

function getHorizontalFOV()
{
    return $ZoomOn ? $Pref::player::CurrentFOV : $Pref::Player::defaultFov;
}

function getVerticalFOV()
{
    return $verticalFOV;
}

//
// FOV and Resolution change support.
//

function invalidateResolutionCaches()
{
    $resolutionHash = $pref::Video::resolution;
    $resolution = getWords($pref::Video::resolution, 0, 1);
    $resolutionX = getWord($resolution, 0);
    $resolutionY = getWord($resolution, 1);
    $aspectRatio = ($resolutionX / $resolutionY);
}

function invalidateFOVCaches()
{
    $horizontalFOVHash = $Pref::Player::defaultFov;
    $horizontalFOV = $Pref::Player::defaultFov;
    $verticalFOV = calculateVerticalFOV($horizontalFOV, $aspectRatio);
}

package Client_Eventide_Scaling
{
    function optionsDlg::updateFOV(%this)
    {
        parent::updateFOV(%this);
        invalidateFOVCaches();
    }

    function optionsDlg::applyGraphics(%this)
    {
        parent::applyGraphics(%this);
        invalidateResolutionCaches();
    }
};
if(isPackage(Client_Eventide_Scaling))
{
    deactivatePackage(Client_Eventide_Scaling);
}
activatePackage(Client_Eventide_Scaling);