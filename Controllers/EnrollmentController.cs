using System.Threading.Tasks;
using System.Web.Mvc;
using FormsSandbox.Forms2.Examples;

namespace FormsSandbox.Controllers
{
    public sealed class EnrollmentController : Controller
    {
        [HttpGet]
        public ActionResult Edit() => View(new EnrollmentModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> SaveDraft() => Process("SaveDraft");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> Submit() => Process("Submit");

        private async Task<ActionResult> Process(string action)
        {
            var definition = EnrollmentDefinition.Create();
            var result = await definition.ValidateAsync(action, Request.Form.GetValues, ModelState);
            if (!result.IsValid || !ModelState.IsValid)
            {
                foreach (var field in result.Fields.Values)
                    ModelState.SetModelValue(field.Name,
                        new ValueProviderResult(field.Raw, field.Raw,
                            System.Globalization.CultureInfo.InvariantCulture));
                return View("Edit", new EnrollmentModel());
            }
            // Only the validated, normalized typed result.Model may be used for writes.
            TempData["Notice"] = action + " accepted for " + result.Model.Plan + ". No data was saved.";
            return RedirectToAction("Edit");
        }
    }
}