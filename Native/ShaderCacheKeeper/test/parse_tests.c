/* Unit tests for keeper.c's text parsing, on in-memory buffers. Built and run by run_tests.ps1. */
#include "../keeper.c"

static int failures;

static void expect(const char *what, const char *got, const char *want)
{
    int ok = (got == NULL && want == NULL) || (got && want && !strcmp(got, want));
    printf("%s %s (got %s)\n", ok ? "ok  " : "FAIL", what, got ? got : "NULL");
    if (!ok) failures++;
}

static char *parse(const char *text)
{
    char *copy = _strdup(text), *list = list_from_launcher_xml(copy);
    free(copy);
    return list;
}

#define MOD(id, selected) "<UserModData><Id>" id "</Id><IsSelected>" selected "</IsSelected></UserModData>"

int main(void)
{
    char *list;
    list = parse("<UserData><GameType>Singleplayer</GameType><SingleplayerData><ModDatas>" MOD("TAOM", "true")
                 MOD("Sandbox", "true") MOD("Off", "false") MOD("SandBoxCore", "true")
                 "</ModDatas></SingleplayerData><MultiplayerData><ModDatas>" MOD("Mp", "true")
                 "</ModDatas></MultiplayerData></UserData>");
    expect("single player selection, sorted by character code", list, "SandBoxCore;Sandbox;TAOM");
    free(list);

    list = parse("<UserData><GameType>Multiplayer</GameType><SingleplayerData><ModDatas>" MOD("TAOM", "true")
                 "</ModDatas></SingleplayerData><MultiplayerData><ModDatas>" MOD("Mp", "true")
                 "</ModDatas></MultiplayerData></UserData>");
    expect("a multiplayer start gives no list", list, NULL);
    free(list);

    list = parse("<UserData><SingleplayerData><ModDatas>" MOD("TAOM", "true") "</ModDatas></SingleplayerData></UserData>");
    expect("no GameType gives no list", list, NULL);
    free(list);

    list = parse("<UserData><GameType>Singleplayer</GameType><SingleplayerData><ModDatas>" MOD("TAOM", "false")
                 "</ModDatas></SingleplayerData></UserData>");
    expect("nothing selected gives no list", list, NULL);
    free(list);

    printf("%d failed\n", failures);
    return failures;
}
