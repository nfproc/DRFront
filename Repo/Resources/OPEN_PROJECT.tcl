common::send_msg_id "DRFront-001" "INFO" "Running an open-project script generated by DRFront..."
set script_mode $rdi::mode
if { $script_mode ne "gui" } {
  start_gui
}
cd [ file dirname [ file normalize [ info script ] ] ]
if { [ current_project -quiet ] ne "" } {
  common::send_msg_id "DRFront-002" "INFO" "Closing currently opened project."
  while { [ current_sim -quiet ] ne ""} {
    close_sim
  }
  close_project
}

namespace eval _tcl {
  proc add_fileset {} {
    global source_files testbench_file
    add_files -norecurse $source_files
    set_property SOURCE_SET sources_1 [ get_filesets sim_1 ]
    add_files -fileset sim_1 -norecurse $testbench_file
    set_property top DR_TOP [ get_filesets sources_1 ]
    set_property TOP DR_TESTBENCH [ get_filesets sim_1 ]
    update_compile_order -force_gui
    return 0
  }
}

if { [ file exists ${project_name}.xpr ] == 1 } {
  # Open Project
  common::send_msg_id "DRFront-003" "INFO" "Project file exists. Opening the project..."
  open_project ${project_name}.xpr

  # Recreate Fileset if needed
  set proj_files [ get_files -quiet $source_files ]
  if { [ llength $source_files ] != [ llength $proj_files ] } {
    common::send_msg_id "DRFront-005" "INFO" "Fileset has been changed and needs to be recreated."
    remove_files [ get_files ]
    _tcl::add_fileset
  }

} else {
  # Create Project
  common::send_msg_id "DRFront-004" "INFO" "Project file does not exist. Creating the project..."
  create_project -force $project_name . -part $target_fpga
  if { [ llength [ get_board_parts -quiet $target_board ] ] == 1 } {
    set_property board_part $target_board [ current_project ]
  } else {
    common::send_msg_id "DRFront-006" "INFO" "Board file is not found. Board part name was not set."
  }
  _tcl::add_fileset
  set_property STEPS.SYNTH_DESIGN.ARGS.FLATTEN_HIERARCHY none [ get_runs synth_1 ]
  set_property -name {STEPS.SYNTH_DESIGN.ARGS.MORE OPTIONS} -value {-mode out_of_context} -objects [ get_runs synth_1 ]
}