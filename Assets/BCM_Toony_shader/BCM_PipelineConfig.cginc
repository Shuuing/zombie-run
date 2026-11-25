// BCM_Toony_shader - pipeline configuration (auto-managed)
//
// This file is rewritten automatically by Editor/BCM_PipelineConfigurator.cs depending on whether
// the Universal Render Pipeline package is installed in the project:
//   - URP installed     -> "#define BCM_URP"   (the URP SubShader compiles its real URP code)
//   - URP NOT installed -> line commented out   (the URP SubShader compiles a harmless stub so the
//                                                shader imports without errors in Built-in RP projects)
//
// You normally never edit this by hand. If you move the folder to a new project, the configurator
// runs on load and sets the correct value automatically. You can also force it from the menu:
//   Tools > BCM > Reconfigure Toony Shader Pipeline

#define BCM_URP
