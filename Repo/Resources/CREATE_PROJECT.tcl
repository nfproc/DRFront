start_gui
create_project -force $project_name . -part xc7a100tcsg324-1
set_property board_part digilentinc.com:nexys-a7-100t:part0:1.0 [current_project]
add_files -norecurse $source_files
set_property top DR_TOP [get_filesets sources_1]
update_compile_order -fileset sources_1
set_property STEPS.SYNTH_DESIGN.ARGS.FLATTEN_HIERARCHY none [get_runs synth_1]
set_property -name {STEPS.SYNTH_DESIGN.ARGS.MORE OPTIONS} -value {-mode out_of_context} -objects [get_runs synth_1]
