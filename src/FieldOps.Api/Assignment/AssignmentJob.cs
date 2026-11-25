using FieldOps.Api.Domain;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Assignment;

// Bir iş için ekip aramaya başlarken lazım olan her şey (veritabanından okunmuş, sadece veri taşır).
// Request: eleme fonksiyonunun istediği iş paketi (Urgency = işin GÜNCEL aciliyeti).
// Location: ekip fotoğraflarını hazırlayan TeamSnapshotLoader'ın mesafe hesabı için.
// Status: işin güncel durumu; zaten yolda olan bir işe ekip önerilmesin diye çağıran kod buna bakar.
public sealed record AssignmentJob(JobRequest Request, Point Location, JobStatus Status);