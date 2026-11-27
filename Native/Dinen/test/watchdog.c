/* Test stand-in for Watchdog.exe: shows the crash question the way Watchdog does and exits with the button that closed
 * it (IDNO = 7). The same program under another name checks that the proxy leaves other programs' boxes alone. */
#include <windows.h>

int main(void)
{
    return MessageBoxA(NULL, "The application faced a problem. We need to collect necessary files to fix this problem. "
                             "Would you like to proceed?", "*_*", MB_YESNO | MB_ICONQUESTION);
}
