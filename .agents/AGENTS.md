# Workspace Rules

<RULE[user_global]>
## NO SILENT FALLBACKS (STRICT)
- **Error Visibility**: Never use silent fallbacks (like defaulting to `Vector3.zero`, `null`, or ignoring an error) when a required component, reference, or value is missing.
- **Fail Loudly**: Always use `Debug.LogError` or throw an exception to make the failure immediately visible to the developer.
- **Why**: Silent failures hide configuration errors, leading to unpredictable bugs that are extremely hard to track down later. Fail fast, fail loudly.
</RULE[user_global]>
