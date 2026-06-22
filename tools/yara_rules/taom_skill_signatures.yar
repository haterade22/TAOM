/*
    TAOM clean-room malware / webshell / C2 / cryptominer / hacktool signatures
    for scanning Claude Code skill artifacts (markdown, shell, python, json).

    Authored from scratch for TAOM. These rules match well-known, publicly
    documented indicators of compromise (tool names, protocol strings, syscall
    shapes) — facts, not copyrightable expression. They are NOT derived from,
    and do NOT reproduce, the Neo23x0/signature-base (DRL-1.1) or other upstream
    .yar files bundled with NVIDIA SkillSpector; that content was deliberately
    NOT vendored (license: DRL-1.1 / unstated). See
    docs/reviews/adopt-skillspector-2026-06-22.md.

    Loaded (optionally) by tools/audit_claude_config.py when `yara-python` is
    importable. The auditor runs fully without it; this layer is pure upside.

    License: same as the TAOM repository.
*/

rule taom_reverse_shell
{
    meta:
        category = "yara-malware"
        severity = "CRITICAL"
        description = "Reverse-shell construction in a skill artifact"
    strings:
        $bash_devtcp  = /bash\s+-i\s+>&?\s*\/dev\/tcp\// nocase
        $nc_exec      = /\bn(cat|c)\b[^\n]{0,40}-e\s*\/bin\/(ba)?sh/ nocase
        $socat_exec   = /socat\b[^\n]{0,60}EXEC[^\n]{0,20}\/bin\/(ba)?sh/ nocase
        $mkfifo_sh    = /mkfifo\b[^\n]{0,60}\|\s*\/bin\/(ba)?sh/ nocase
        $py_sock      = /socket\.socket\([^\n]{0,80}SOCK_STREAM[^\n]{0,120}\.connect\(/
        $ps_tcpclient = /New-Object\s+System\.Net\.Sockets\.TCPClient/ nocase
    condition:
        any of them
}

rule taom_webshell
{
    meta:
        category = "yara-webshell"
        severity = "CRITICAL"
        description = "Webshell pattern (PHP/Python request-driven exec) or known shell family"
    strings:
        $php_eval_req   = /(eval|assert|system|passthru|shell_exec|popen|proc_open)\s*\(\s*\$_(POST|GET|REQUEST|COOKIE)\s*\[/ nocase
        $php_b64_eval   = /eval\s*\(\s*(base64_decode|gzinflate|gzuncompress|str_rot13)\s*\(/ nocase
        $py_exec_req    = /(exec|eval)\s*\(\s*request\.(args|form|data|json|values)/ nocase
        $py_os_req      = /os\.(system|popen)\s*\(\s*request\./ nocase
        $fam_c99        = "c99shell" nocase
        $fam_r57        = "r57shell" nocase
        $fam_wso        = "Web Shell by" nocase
        $fam_chopper    = "China Chopper" nocase
        $fam_godzilla   = "GodzillaShell" nocase
        $fam_antsword   = "antSword" nocase
        $fam_behinder   = "behinder" nocase
    condition:
        any of them
}

rule taom_c2_framework
{
    meta:
        category = "yara-malware"
        severity = "CRITICAL"
        description = "Command-and-control framework references"
    strings:
        $cobalt   = "cobaltstrike" nocase
        $meter    = "meterpreter" nocase
        $msf      = /metasploit[^\n]{0,40}(payload|exploit|stager)/ nocase
        $sliver   = /sliver[^\n]{0,30}(implant|beacon|session)/ nocase
        $empire   = /powershell[^\n]{0,30}empire/ nocase
        $havoc    = /havoc[^\n]{0,30}(demon|teamserver)/ nocase
        $beacon   = /C2Server\s*[=:]/ nocase
    condition:
        any of them
}

rule taom_info_stealer
{
    meta:
        category = "yara-malware"
        severity = "HIGH"
        description = "Credential / browser-data / wallet theft indicators"
    strings:
        $mimikatz   = "mimikatz" nocase
        $lazagne    = "lazagne" nocase
        $wallet     = "wallet.dat" nocase
        $chrome_db  = /Chrome[^\n]{0,40}Login\s*Data/ nocase
        $ff_logins  = /logins\.json[^\n]{0,40}firefox/ nocase
        $ff_logins2 = /firefox[^\n]{0,40}logins\.json/ nocase
        $ntds       = "ntds.dit" nocase
        $sam_save   = /reg\s+save\b[^\n]{0,40}\\sam\b/ nocase
    condition:
        any of them
}

rule taom_cryptominer
{
    meta:
        category = "yara-malware"
        severity = "HIGH"
        description = "Cryptocurrency miner / cryptojacking indicators"
    strings:
        $stratum    = /stratum\+tcp:\/\// nocase
        $xmrig      = "xmrig" nocase
        $cryptonight = "cryptonight" nocase
        $coinhive   = "coinhive" nocase
        $minerd     = /\bminerd\b/ nocase
        $nicehash   = "nicehash" nocase
    condition:
        any of them
}

rule taom_backdoor_persistence
{
    meta:
        category = "yara-malware"
        severity = "HIGH"
        description = "Persistence via authorized_keys / shell-rc / LD_PRELOAD / hidden root user"
    strings:
        $ssh_inject   = /echo\b[^\n]{0,80}>>?\s*[^\n]{0,40}\.ssh\/authorized_keys/ nocase
        $rc_persist   = /echo\b[^\n]{0,80}>>?\s*[^\n]{0,40}\.(bashrc|zshrc|profile|bash_profile)/ nocase
        $ld_preload   = /LD_PRELOAD\b[^\n]{0,40}\.so\b/ nocase
        $hidden_root  = /useradd\b[^\n]{0,40}-o\b[^\n]{0,20}-u\s*0\b/ nocase
    condition:
        any of them
}

rule taom_hacktool
{
    meta:
        category = "yara-hacktool"
        severity = "MEDIUM"
        description = "Offensive-tool / exploit-framework references that should not appear in a skill"
    strings:
        $sqlmap   = /\bsqlmap\b/ nocase
        $nmap_sn  = /\bnmap\b[^\n]{0,30}-s[A-Za-z]/ nocase
        $hydra    = /\bhydra\b[^\n]{0,30}-[lLpP]/ nocase
        $hashcat  = /\bhashcat\b[^\n]{0,30}-m\b/ nocase
        $john     = /\bjohn\b[^\n]{0,30}--wordlist/ nocase
        $msfvenom = "msfvenom" nocase
    condition:
        any of them
}
