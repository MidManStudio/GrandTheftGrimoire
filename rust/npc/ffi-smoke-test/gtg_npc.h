#ifndef GTG_NPC_H
#define GTG_NPC_H
#include <stdint.h>
#define GTG_NPC_ABI_VERSION 2u
#define GTG_NPC_MAX_BATCH 4096u
#define GTG_NPC_OK 0
#define GTG_NPC_NULL -1
#define GTG_NPC_CAPACITY -2
#define GTG_NPC_INVALID -3
typedef struct {
    uint64_t npc_id;
    uint32_t role, backend, threat_visible;
    float health_fraction;
    uint32_t can_move, reserved;
} GtgNpcObservation;
typedef struct { uint64_t npc_id; int32_t action; uint32_t reserved; } GtgNpcDecision;
uint32_t gtg_npc_abi_version(void);
uint32_t gtg_npc_observation_size(void);
uint32_t gtg_npc_decision_size(void);
int32_t gtg_npc_decide_batch(const GtgNpcObservation *inputs, uint32_t count, GtgNpcDecision *outputs, uint32_t capacity);
#endif
