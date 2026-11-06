using System.Web.Mvc;
using FormsSandbox.Examples;
using FormsSandbox.Models;

namespace FormsSandbox.Controllers
{
    public class DocumentController : Controller
    {
        [HttpGet]
        public ActionResult Edit()
        {
            return View(new DocumentForm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveDraft(DocumentForm form)
        {
            var result = DocumentFormDefinition.Create().ValidateValues("SaveDraft",
                name => Request.Form.GetValues(name), ModelState);
            if (!result.IsValid || !ModelState.IsValid)
            {
                return View("Edit", form);
            }

            // This example intentionally has no persistence layer.
            TempData["Notice"] = "Draft accepted. No data has been saved in this example.";
            return RedirectToAction("Edit");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Submit(DocumentForm form)
        {
            var result = DocumentFormDefinition.Create().ValidateValues("Submit",
                name => Request.Form.GetValues(name), ModelState);

            if (!result.IsValid || !ModelState.IsValid)
            {
                return View("Edit", form);
            }

            TempData["Notice"] = "Submission accepted. No data has been saved in this example.";
            return RedirectToAction("Edit");
        }

    }
}