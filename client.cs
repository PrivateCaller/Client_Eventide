//Execute embedded custom CDN support, if the client doesn't already have it.
if($CustomCDN::clientVersion $= "")
{
    exec("./modules/scripts/support_customcdn.cs");
}

exec("./modules/scripts/support_scaling.cs"); //Resolution and FOV support code.
exec("./modules/scripts/script_billboards.cs");