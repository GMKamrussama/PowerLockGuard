# Codex দিয়ে একটি বাস্তব Windows সফটওয়্যারের নিরাপত্তা সমস্যা কীভাবে সমাধান করলাম

PowerLockGuard একটি offline Windows utility: Charger Guard disconnect protection দেয়; Work Watchdog keep-awake এবং inactivity/grace ব্যবহার করে। বর্তমান application source `8b7fdd41e6eaaf9de68080d9e4cfd6c7deecda1f`-এ review fixes-সহ স্থানীয় Debug/Release প্রতিটিতে **১০৩টি পাস, ০ ব্যর্থ**; solution builds, Build.bat ও CLI পাস। [PR #3](https://github.com/GMKamrussama/PowerLockGuard/pull/3)-তে exact-head hosted checks ও review দেখুন। [TEST_RESULTS](TEST_RESULTS.md) canonical evidence; পুরোনো ৩৭/৪০/৬৬/৭৬ ফল নির্দিষ্ট source-এর ইতিহাস।

আগের approved build-এর charger, input cancellation ও Sleep/Resume মালিক পরীক্ষা করেছেন: **OWNER-TESTED: SUCCESS**। Stage 1-এর Windows-visible lease/release এবং resumed-work cancellation measurements `6568f0d` candidate-এর; বর্তমান `8b7fdd4`-এর ১০৩-test local verification আলাদা। নতুন hash-এর physical acceptance বা production Codex completion সেই ফল থেকে ধরে নেওয়া হয়নি। বিস্তারিত [TEST_RESULTS](TEST_RESULTS.md)-এ আছে।

Work Watchdog-এ CPU heuristic আগের সীমাবদ্ধতা নিয়েই আছে। Registered tasks mode-এ নিজে চালু করা finite command বা Codex `exec --json` session-এর running handle/lifecycle ব্যবহার হয়; কম CPU মানেই completion নয়। সব registered কাজ সফলভাবে শেষ, healthy monitoring এবং প্রয়োজনীয় inactivity/grace ছাড়া automatic action হয় না। Unknown status automatic action আটকায়; আগে জানা চলমান কাজের keep-awake ধরে রাখা বা ছেড়ে দেওয়ার policy নির্বাচন করা যায়। সাধারণ VS Code/Antigravity window-এর কাজ শেষ শনাক্ত করার integration নেই। Live warning preview-এর সময়ও enabled Charger Guard সক্রিয়; সম্পূর্ণ নিরাপদ প্রদর্শনের জন্য `--dry-run` ব্যবহার করুন।

মালিকের অনুমোদিত public technical documentation। [PR #1](https://github.com/GMKamrussama/PowerLockGuard/pull/1) থেকে v1.1 source `master`-এ [70cd1b8ac8ad2441c6ac75b87124aac500ce3f36](https://github.com/GMKamrussama/PowerLockGuard/commit/70cd1b8ac8ad2441c6ac75b87124aac500ce3f36) commit-এ merge হয়েছে। Automated Windows validation সম্পন্ন; v1.1 binary release এখনো প্রকাশিত হয়নি। ওই historical hosted run physical behavior বা Codex completion প্রমাণ করে না; বর্তমান owner ও candidate evidence উপরে আলাদা আছে।

## সমস্যাটি কী ছিল

PowerLockGuard একটি offline Windows utility। নির্বাচিত developer process-এর CPU inactivity কিছু সময় থাকলে এটি power action-এর প্রার্থী তৈরি করে। Charger Guard আলাদাভাবে AC বিচ্ছিন্ন হওয়া দেখে। রাতের build, Python job বা coding agent চালিয়ে রাখার ক্ষেত্রে এটি কাজে লাগতে পারে, কিন্তু ভুল সিদ্ধান্ত নিলে চলমান কাজ থামিয়ে দিতে পারে।

প্রথম সংস্করণ Antigravity-এর সহায়তায় তৈরি হয়েছিল বলে প্রকল্পের লেখক জানিয়েছেন। পরের safety audit, refinement ও পরীক্ষায় Codex ব্যবহার করা হয়েছে। পুরো মূল অ্যাপ Codex বানিয়েছে—এমন দাবি এখানে নেই।

## CPU দেখে কী বোঝা যায়, আর কী যায় না

বেশি CPU মানেই process আটকে গেছে নয়। Compile, test ও training স্বাভাবিকভাবেই ব্যস্ত থাকতে পারে। আবার কম CPU মানেই কাজ শেষ নয়: agent network response, user approval বা child process-এর জন্য অপেক্ষা করতে পারে।

পুরোনো সোর্সে high CPU ও ব্যবহারকারীর অনুপস্থিতি মিলে automatic kill-এর শর্ত তৈরি হতো। Countdown বন্ধ করার default ফলও নিরাপদ ছিল না। Charger Unknown-কে unplug মনে করা সম্ভব ছিল। এগুলো কোডে পাওয়া সমস্যা; কোনো নির্দিষ্ট ব্যবহারকারীর ক্ষতি হয়েছে বলে দাবি করা হচ্ছে না।

## Codex দিয়ে কী করা হয়েছে

প্রথমে পুরোনো executable না চালিয়ে source ও action call path পড়া হয়। তারপর আলাদা branch/worktree-তে ছোট পরিবর্তন, fake dependency, regression assertion ও Windows build automation যোগ করা হয়। ব্যর্থ test-এর কারণও দেখা হয়—শুধু সবুজ ফল বানানোর জন্য test সরিয়ে দেওয়া হয়নি।

v1.1-এ CPU-based auto-kill বন্ধ। Manual termination-এর আগে confirmation এবং PID + start time যাচাই থাকে। Unknown বা incomplete sample idle সময় বাড়ায় না। Countdown default cancel; কাজ ফিরে এলে, settings বদলালে বা শেষ sample অনিশ্চিত হলে action বাতিল হয়। Charger Unknown confirmation ভেঙে দেয়; নিশ্চিত Offline observation debounce পেরোলে একবার request হয়।

শেষ local audit-এ দেখা যায় keyboard/mouse helper থাকা সত্ত্বেও eligibility-তে সেটি ব্যবহার হয়নি। নতুন follow-up automatic watchdog action-এর আগে input inactivity যাচাই করে। নতুন input বা sensor failure automatic action আটকায়। Explicit Execute Now manual সিদ্ধান্ত হিসেবে থাকে; সেটিও fresh CPU/sample পরীক্ষা এড়ায় না। Charger Guard-এর independent unplug action অপরিবর্তিত।

## নিরাপদে নিজে পরীক্ষা করুন

Windows-এ এই checkout থেকে:

```powershell
$env:POWERLOCKGUARD_NO_NATIVE_ACTIONS = '1'
powershell -NoProfile -ExecutionPolicy Bypass -File .\ci\build-and-test.ps1
& .\bin\Verification\Release\PowerLockGuard.exe --dry-run
```

Title-এ SIMULATION দেখতে হবে। এখানে process ও charger input synthetic; real action executor তৈরি হয় না। Watchdog enable করলে fake workload নতুন করে শুরু হয়। Settings & Logs-এর Test Warning Dialog খুলে Cancel/Esc বা X-এর আচরণ দেখা যায়। Unflagged EXE double-click করবেন না—সেটি live mode।

দ্রুত headless trace চাইলে:

```powershell
$p = Start-Process .\bin\Verification\Release\PowerLockGuard.exe -ArgumentList '--simulate all' -WindowStyle Hidden -Wait -PassThru
$p.ExitCode
Get-Content .\artifacts\simulation.log
```

এটি virtual-time policy trace; বাস্তবে ৩০ সেকেন্ড অপেক্ষা বা power transition-এর recording নয়।

## কোন evidence আছে

Merged `70cd1b8` snapshot-এর [post-merge Windows CI](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40509133147)-তে Debug/Release প্রতিটিতে ৪০টি পাস, ০ ব্যর্থ। Download করা দুই report-এর প্রতিটিতে ৪০টি PASS entry আছে; solution builds ও Build.bat পাস এবং native-action interlock সক্রিয় ছিল। এটি source merge ও automated validation-এর প্রমাণ; v1.1 binary প্রকাশ বা hardware validation-এর প্রমাণ নয়।

[PR #1](https://github.com/GMKamrussama/PowerLockGuard/pull/1)-এর historical `d829004` snapshot-এ Debug/Release-এ ৩৭টি করে test পাস করেছিল। আগের যাচাইকৃত hosted snapshot `04a0fe411ba09ab3ee399e2a28e6d88ec2531ffa`-এর [PR run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40503655880) ও [push run](https://github.com/GMKamrussama/PowerLockGuard/actions/runs/40503648420)-এ Debug/Release প্রতিটিতে ৪০টি পাস, ০ ব্যর্থ; solution builds ও Build.bat-ও পাস। Download করা চারটি configuration report-এর প্রতিটিতে ৪০টি PASS entry যাচাই করা হয়েছে। ওই পুরোনো documentation update application/test source বদলায়নি; বর্তমান core review-তে application/test পরিবর্তন আছে। পরের head-এর ফল [Windows workflow runs](https://github.com/GMKamrussama/PowerLockGuard/actions/workflows/windows-safety.yml)-এ দেখুন। বিস্তারিত [test matrix](TEST_RESULTS.md) ও [case study](CODEX_CASE_STUDY.md)-তে আছে।

Automated candidate tests-এ real sleep/hibernate/shutdown/lock/process-kill হয়নি। Stage 1-এ শুধু অনুমোদিত real keep-awake lease এবং read-only diagnostic চালানো হয়েছে। আগের build-এর charger/input/Sleep-Resume মালিকের পরীক্ষা সফল; নতুন hash-এর physical power/input acceptance বাকি। S3 capability report, idle inhibition ও session/token preservation আলাদা দাবি। Unsigned candidate signed release নয়।

## অন্য প্রকল্পে কাজে লাগানোর শিক্ষা

Unknown-কে Idle বানাবেন না। Destructive API-র আগে একটি কেন্দ্রীয় boundary রাখুন। PID-এর সঙ্গে instance identity যাচাই করুন। Clock ও sensor inject করলে অপেক্ষা বা বাস্তব power action ছাড়াই failure path পরীক্ষা করা যায়। Positive path-ও পরীক্ষা করুন: fixture-এর ভুল target change থেকে cancellation হলেও শুধু “কিছু হয়নি” assertion পাস করতে পারে।

Codex-এর সহায়তায় কাজ করলেও test, source review ও মানুষের সিদ্ধান্ত দরকার। একটি CI badge hardware validation বা task completion integration-এর বিকল্প নয়। এই tutorial-এর লক্ষ্য reproducible engineering শেখানো; OpenAI endorsement বা Ambassador selection-এর দাবি করা নয়।
