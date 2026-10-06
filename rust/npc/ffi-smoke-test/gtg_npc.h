// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/gtg-npc-ffi.md, section "gtg_npc.h"
// ============================================================================
#ifndef GTG_NPC_H
#define GTG_NPC_H
#include <stdint.h>

/* Must equal ABI_VERSION in rust/npc/crates/npc-ffi/src/lib.rs. */
#define GTG_NPC_ABI_VERSION 3u
#define GTG_NPC_MAX_BATCH 4096u

/* Status codes returned by gtg_npc_decide_batch. */
#define GTG_NPC_OK 0
#define GTG_NPC_NULL -1     /* a pointer is null or not aligned */
#define GTG_NPC_CAPACITY -2 /* count above the maximum, or output smaller than input */
#define GTG_NPC_INVALID -3  /* unknown value, non-finite number, non-zero reserved field, or overlapping buffers */

/* Field values. The numbers are the ABI. */
#define GTG_NPC_ROLE_MERCHANT 0
#define GTG_NPC_ROLE_CIVILIAN 1
#define GTG_NPC_ROLE_GUARD 2
#define GTG_NPC_ROLE_ENEMY 3
#define GTG_NPC_ROLE_BOSS 4
#define GTG_NPC_ROLE_COMPANION 5
#define GTG_NPC_BACKEND_STATE_MACHINE 0
#define GTG_NPC_BACKEND_UTILITY 1
#define GTG_NPC_BACKEND_HYBRID_ML 2
#define GTG_NPC_DISPOSITION_HOSTILE 0
#define GTG_NPC_DISPOSITION_RETALIATORY 1
#define GTG_NPC_DISPOSITION_PEACEFUL 2
#define GTG_NPC_ORDER_NONE 0
#define GTG_NPC_ORDER_FOLLOW 1
#define GTG_NPC_ORDER_HOLD 2
#define GTG_NPC_ORDER_ATTACK 3
#define GTG_NPC_ACTION_IDLE 0
#define GTG_NPC_ACTION_TRADE 1
#define GTG_NPC_ACTION_PATROL 2
#define GTG_NPC_ACTION_ATTACK 3
#define GTG_NPC_ACTION_RETREAT 4
#define GTG_NPC_ACTION_FOLLOW 5
#define GTG_NPC_ACTION_HOLD 6
#define GTG_NPC_ACTION_REFUSE_ORDER 7

/* One NPC, 64 bytes. Booleans are 0 or 1. Every reserved field must be zero. A zero in every field
   after `reserved` means: hostile, no order, not provoked, level 0. */
typedef struct {
    uint64_t npc_id;
    uint32_t role, backend, threat_visible;
    float health_fraction;
    uint32_t can_move, reserved;
    uint8_t disposition, order, provoked, reserved_byte;
    uint16_t level, order_level;
    uint64_t reserved_tail[3];
} GtgNpcObservation;

/* One decision, 16 bytes. `reserved` is always zero. */
typedef struct { uint64_t npc_id; int32_t action; uint32_t reserved; } GtgNpcDecision;

uint32_t gtg_npc_abi_version(void);
uint32_t gtg_npc_observation_size(void);
uint32_t gtg_npc_decision_size(void);
int32_t gtg_npc_decide_batch(const GtgNpcObservation *inputs, uint32_t count, GtgNpcDecision *outputs, uint32_t capacity);
#endif
