namespace RealEstatePortal.Web;

// Marker type only — it names the .resx file (Resources/SharedResource.*.resx) that every view
// localizes against. One shared file rather than one per view: the same handful of words
// ("Save", "Cancel", "Price") appear on a dozen screens, and per-view files would translate
// each of them a dozen times.
//
// Keys are the English text itself. That keeps views readable and, more importantly, makes a
// missing translation fall back to correct English rather than showing a raw key to a visitor.
public class SharedResource
{
}
