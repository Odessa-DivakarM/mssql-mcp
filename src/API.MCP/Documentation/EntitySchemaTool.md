# EntitySchemaTool Documentation

This document contains the full usage, workflow, and example documentation for the `EntitySchemaTool` class, extracted from the codebase. It covers schema discovery, AI-first design, usage patterns, and output structure. Please refer to this file for all usage and workflow details previously found in the class XML documentation.

---

[Full documentation content extracted from the class:]

## Overview
Entity schema tool for discovering and understanding entity structures from XML definitions.

### AI-FIRST DESIGN APPROACH
This tool provides structured, AI-friendly data output. The AI should handle:
- Complex formatting and presentation logic
- Smart pluralization (beyond simple "add s" rule)
- Context-sensitive error messages and suggestions
- Intelligent schema interpretation and recommendations

### CORE USAGE PATTERNS
- GetEntitySchema() ? Get structured entity information
- GetAvailableEntities() ? List all entities with persistence status
- Use schema data to validate GetEntityData() parameters

The tool returns concise, structured information that AI can enhance with:
- Better formatting, explanations, and examples
- Context-aware suggestions and error recovery
- Smart relationship analysis and recommendations

---

(See the code for essential XML summaries only.)
