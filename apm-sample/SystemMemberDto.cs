using System;
namespace apm_sample
{
    public class SystemMemberDto
    {
        public int Id { get; set; }  // Corresponds to the Id field in the SQL table
        public string RoleName { get; set; }  // Corresponds to the RoleName field
        public string EmpNo { get; set; }  // Corresponds to the EmpNo field
        public string UserId { get; set; }  // Corresponds to the UserId field
        public DateTime UpdateTime { get; set; }  // Corresponds to the UpdateTime field

    }
}

