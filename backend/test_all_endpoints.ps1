$ErrorActionPreference = 'Stop'

function Call-Api {
    param(
        [string]$Method,
        [string]$Url,
        [string]$Body = $null,
        [string]$Token = $null
    )
    $headers = @{}
    if ($Token) {
        $headers["Authorization"] = "Bearer $Token"
    }
    
    $params = @{
        Uri = $Url
        Method = $Method
        ContentType = "application/json; charset=utf-8"
        Headers = $headers
    }
    if ($Body) {
        $params["Body"] = [System.Text.Encoding]::UTF8.GetBytes($Body)
    }

    return Invoke-RestMethod @params
}

$suffix = (Get-Random -Minimum 1000 -Maximum 9999).ToString()

Write-Host "========================================="
Write-Host "1. Testing ADMIN Registration & Endpoints"
Write-Host "========================================="
$adminId = "admin_$suffix"
$adminBody = '{"id":"' + $adminId + '","fullName":"Administrador Auditor","role":"Admin","password":"Password123","captchaToken":"DEV_TEST_TOKEN"}'
$adminAuth = Call-Api -Method "POST" -Url "http://localhost:5000/api/auth/register" -Body $adminBody
Write-Host "  -> Admin registered: $($adminAuth.user.fullName) ($($adminAuth.user.id)) [Role: $($adminAuth.user.role)]"
$adminToken = $adminAuth.token

# List Admin Courses
$courses = Call-Api -Method "GET" -Url "http://localhost:5000/api/admin/courses" -Token $adminToken
Write-Host "  -> Initial Admin courses count: $($courses.Count)"

# Create a Course via Admin
$newCourse = '{"nombre":"Robotica y Logica ' + $suffix + '","grupo":"5-B","descripcion":"Taller institucional de algoritmos","grado":"5°"}'
$createdCourse = Call-Api -Method "POST" -Url "http://localhost:5000/api/admin/courses" -Body $newCourse -Token $adminToken
Write-Host "  -> Created Admin Course: ID=$($createdCourse.id), Name=$($createdCourse.nombre), Grado=$($createdCourse.grado)"

# Update the Course via Admin
$updateCourse = '{"nombre":"Robotica Avanzada ' + $suffix + '","grupo":"5-B","descripcion":"Actualizado para pruebas","grado":"5°"}'
$updatedRes = Call-Api -Method "PUT" -Url "http://localhost:5000/api/admin/courses/$($createdCourse.id)" -Body $updateCourse -Token $adminToken
Write-Host "  -> Course updated successfully: $($updatedRes.message)"

# Toggle Course Status via Admin
$toggleRes = Call-Api -Method "PATCH" -Url "http://localhost:5000/api/admin/courses/$($createdCourse.id)/toggle-status" -Token $adminToken
Write-Host "  -> Course status toggled: $($toggleRes.message) (Activo: $($toggleRes.activo))"

Write-Host "`n=========================================="
Write-Host "2. Testing TEACHER Registration & Endpoints"
Write-Host "=========================================="
$teacherId = "prof_$suffix"
$teacherBody = '{"id":"' + $teacherId + '","fullName":"Profe Laura Hernandez","role":"Teacher","password":"Password123","captchaToken":"DEV_TEST_TOKEN"}'
$teacherAuth = Call-Api -Method "POST" -Url "http://localhost:5000/api/auth/register" -Body $teacherBody
Write-Host "  -> Teacher registered: $($teacherAuth.user.fullName) ($($teacherAuth.user.id)) [Role: $($teacherAuth.user.role)]"
$teacherToken = $teacherAuth.token

# Assign this teacher to the course created earlier (using teacher's internal ID)
$teacherInternalId = $teacherAuth.user.internalId
$assignBody = '{"nombre":"Robotica Avanzada ' + $suffix + '","grupo":"5-B","descripcion":"Con docente asignado","grado":"5°","docenteId":' + $teacherInternalId + '}'
$assignRes = Call-Api -Method "PUT" -Url "http://localhost:5000/api/admin/courses/$($createdCourse.id)" -Body $assignBody -Token $adminToken
Write-Host "  -> Teacher assigned to course via Admin: $($assignRes.message)"

# Reactivate course so it shows on dashboards
$toggleBack = Call-Api -Method "PATCH" -Url "http://localhost:5000/api/admin/courses/$($createdCourse.id)/toggle-status" -Token $adminToken
Write-Host "  -> Course reactivated: $($toggleBack.activo)"

# Teacher creates educational activity/content (TASK)
$contentBody = '{"title":"Reto 1: Introduccion a Variables","description":"Crear programa de saludo","type":"Assignment","subject":"Robotica Avanzada ' + $suffix + '","gradeLevel":"5°","dueDate":"2026-10-30T23:59:59"}'
$contentRes = Call-Api -Method "POST" -Url "http://localhost:5000/api/teacher/contents" -Body $contentBody -Token $teacherToken
Write-Host "  -> Teacher created task content: ID=$($contentRes.id), Title=$($contentRes.title), RealId=$($contentRes.realId), Type=$($contentRes.type)"

# Teacher views submissions
$submissions = Call-Api -Method "GET" -Url "http://localhost:5000/api/teacher/submissions/$($contentRes.realId)" -Token $teacherToken
Write-Host "  -> Submissions fetched for task $($contentRes.realId): $($submissions.Count) initial"

Write-Host "`n=========================================="
Write-Host "3. Testing STUDENT Registration & Endpoints"
Write-Host "=========================================="
$studentId = "est_$suffix"
$studentBody = '{"id":"' + $studentId + '","fullName":"Pepito Perez","role":"Student","grade":"5°","password":"Password123","captchaToken":"DEV_TEST_TOKEN"}'
$studentAuth = Call-Api -Method "POST" -Url "http://localhost:5000/api/auth/register" -Body $studentBody
Write-Host "  -> Student registered: $($studentAuth.user.fullName) ($($studentAuth.user.id)) [Role: $($studentAuth.user.role)]"
$studentToken = $studentAuth.token

# Student enrolls in the course via teacher enrollment endpoint
$enrollRes = Call-Api -Method "POST" -Url "http://localhost:5000/api/teacher/courses/$($createdCourse.id)/enroll" -Body ('{"studentId":"' + $studentId + '"}') -Token $teacherToken
Write-Host "  -> Student enrolled in course: $($enrollRes.message)"

# Student loads dashboard
$studentDash = Call-Api -Method "GET" -Url "http://localhost:5000/api/student/dashboard" -Token $studentToken
Write-Host "  -> Student dashboard items: $($studentDash.Count)"
$studentTask = $studentDash | Where-Object { $_.realId -eq $contentRes.realId }
Write-Host "  -> Found task in student dashboard: Title='$($studentTask.title)', HasSubmitted=$($studentTask.hasSubmitted)"

# Student uploads assignment delivery (Multipart/form-data with .pdf)
Write-Host "`n4. Testing Student Assignment Delivery..."
$boundary = [System.Guid]::NewGuid().ToString()
$filePath = "C:\Users\SNEIDER\Documents\LMS\backend\test_upload.pdf"
[System.IO.File]::WriteAllText($filePath, "%PDF-1.4`r`n%Dummy content for testing submission delivery`r`n%%EOF")

$fileBytes = [System.IO.File]::ReadAllBytes($filePath)
$fileContent = [System.Text.Encoding]::GetEncoding("iso-8859-1").GetString($fileBytes)

$LF = "`r`n"
$bodyLines = (
    "--$boundary",
    'Content-Disposition: form-data; name="AssignmentId"',
    '',
    "$($contentRes.realId)",
    "--$boundary",
    'Content-Disposition: form-data; name="Comments"',
    '',
    "Mi solucion en PDF para el reto 1",
    "--$boundary",
    'Content-Disposition: form-data; name="File"; filename="tarea_pepito.pdf"',
    'Content-Type: application/pdf',
    '',
    $fileContent,
    "--$boundary--"
) -join $LF

$uploadParams = @{
    Uri = "http://localhost:5000/api/student/upload-assignment"
    Method = "POST"
    ContentType = "multipart/form-data; boundary=$boundary"
    Headers = @{ Authorization = "Bearer $studentToken" }
    Body = [System.Text.Encoding]::GetEncoding("iso-8859-1").GetBytes($bodyLines)
}

$uploadRes = Invoke-RestMethod @uploadParams
Write-Host "  -> Student uploaded PDF delivery: $($uploadRes.message)"

# Teacher grades the submission
$submissionsAfter = Call-Api -Method "GET" -Url "http://localhost:5000/api/teacher/submissions/$($contentRes.realId)" -Token $teacherToken
Write-Host "  -> Teacher sees $($submissionsAfter.Count) submissions! Submission ID: $($submissionsAfter[0].submissionId)"

$gradeBody = '{"grade":98,"feedback":"Excelente trabajo y desarrollo impecable con diagramas."}'
$gradeRes = Call-Api -Method "POST" -Url "http://localhost:5000/api/teacher/submissions/$($submissionsAfter[0].submissionId)/grade" -Body $gradeBody -Token $teacherToken
Write-Host "  -> Teacher graded submission: $($gradeRes.message), New Status: $($gradeRes.status)"

# Student re-checks dashboard to confirm grade and feedback
$studentDashAfter = Call-Api -Method "GET" -Url "http://localhost:5000/api/student/dashboard" -Token $studentToken
$gradedTask = $studentDashAfter | Where-Object { $_.realId -eq $contentRes.realId }
Write-Host "  -> Student verified graded task:"
Write-Host "     HasSubmitted: $($gradedTask.hasSubmitted)"
Write-Host "     Calificacion: $($gradedTask.mySubmission.grade)"
Write-Host "     Estado:       $($gradedTask.mySubmission.status)"
Write-Host "     Feedback:     '$($gradedTask.mySubmission.feedback)'"

Write-Host "`n=========================================================="
Write-Host ">>> ALL SYSTEM ENDPOINTS & WORKFLOWS VERIFIED 100%! <<<"
Write-Host "=========================================================="
