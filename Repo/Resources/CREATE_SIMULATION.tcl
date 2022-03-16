start_gui
create_project -force $project_name . -part xc7a100tcsg324-1
set_property board_part digilentinc.com:nexys-a7-100t:part0:1.0 [current_project]
add_files -norecurse $source_files
set_property SOURCE_SET sources_1 [get_filesets sim_1]
add_files -fileset sim_1 -norecurse $testbench_file
set_property TOP DR_TESTBENCH [get_filesets sim_1]
update_compile_order -fileset sim_1
set_property STEPS.SYNTH_DESIGN.ARGS.FLATTEN_HIERARCHY none [get_runs synth_1]
