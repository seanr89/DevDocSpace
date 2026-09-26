"use client";

import { validateSpecJson } from "@/lib/content-validation";
import { secondaryBtnCls, textareaCls } from "./admin-ui";

// A plain textarea for OpenAPI JSON with live validation and a pretty-print button.
export function SpecJsonEditor({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  const error = value.trim() ? validateSpecJson(value) : null;

  function format() {
    try {
      onChange(JSON.stringify(JSON.parse(value), null, 2));
    } catch {
      // Leave the text alone; the validation message already explains the problem.
    }
  }

  return (
    <div className="space-y-2">
      <textarea
        aria-label="OpenAPI JSON"
        spellCheck={false}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder='{ "openapi": "3.1.0", "info": { "title": "…", "version": "1.0.0" }, "paths": {} }'
        className={`${textareaCls} h-[60vh]`}
      />
      <div className="flex items-center gap-3">
        <button type="button" onClick={format} className={secondaryBtnCls}>
          Format
        </button>
        {error ? <span className="text-sm text-red-600">{error}</span> : value.trim() && <span className="text-sm text-zinc-500">Valid spec JSON.</span>}
      </div>
    </div>
  );
}
