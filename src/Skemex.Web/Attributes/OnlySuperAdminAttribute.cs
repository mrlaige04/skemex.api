using Microsoft.AspNetCore.Mvc;

namespace Skemex.Web.Attributes;

public class OnlySuperAdminAttribute() : TypeFilterAttribute(typeof(OnlySuperAdminFilter));