using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Threading.Tasks;

namespace ZeroMev.Client
{
    public static class Extensions
    {
        public static ValueTask NavigateToFragmentAsync(this NavigationManager navigationManager, IJSRuntime jSRuntime)
        {
            var uri = navigationManager.ToAbsoluteUri(navigationManager.Uri);

            if (uri.Fragment.Length == 0)
            {
                return default;
            }
            return jSRuntime.InvokeVoidAsync("blazorHelpers.scrollToFragment", uri.Fragment.Substring(1));
        }

        public static ValueTask NavigateToElementAsync(this NavigationManager navigationManager, IJSRuntime jSRuntime, string fragment)
        {
            try
            {
                return jSRuntime.InvokeVoidAsync("blazorHelpers.scrollToFragment", fragment);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return ValueTask.CompletedTask;
            }
        }
    }
}